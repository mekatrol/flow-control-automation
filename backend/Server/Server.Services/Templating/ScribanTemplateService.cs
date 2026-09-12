using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;
using Server.Common.Contracts.Templating;
using Server.Common.Errors;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Server.Services.Templating;

internal sealed class ScribanTemplateService : ITemplateService
{
    private const int MaximumLoopCount = 2_000;
    private const int MaximumOutputLength = 1_000_000;
    private const int MaximumRecursionDepth = 64;

    public TemplateValidationResult Validate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var parsed = Template.Parse(template);

        return new([.. parsed.Messages.Select(ToDiagnostic)]);
    }

    public string Render(
        string template,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        var globals = new ScriptObject();

        foreach (var (name, value) in values)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new TemplateRenderException(
                    TemplateError.UnsupportedValue,
                    "Template value names must not be empty.");
            }

            if (string.Equals(name, "json", StringComparison.Ordinal))
            {
                throw new TemplateRenderException(
                    TemplateError.UnsupportedValue,
                    "The template value name 'json' is reserved.");
            }

            globals.SetValue(name, ToScriptValue(value), true);
        }

        return Render(template, globals);
    }

    public string Render(string template, object model)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(model);

        var globals = new ScriptObject();
        globals.Import(model, renamer: StandardMemberRenamer.Default);

        if (globals.ContainsKey("json"))
        {
            throw new TemplateRenderException(
                TemplateError.UnsupportedValue,
                "The template value name 'json' is reserved.");
        }

        return Render(template, globals);
    }

    private static string Render(string template, ScriptObject globals)
    {
        var parsed = Template.Parse(template);

        if (parsed.HasErrors)
        {
            var diagnostics = parsed.Messages.Select(ToDiagnostic).ToArray();

            throw new TemplateRenderException(
                TemplateError.InvalidTemplate,
                "The template contains invalid Scriban syntax.",
                diagnostics);
        }

        globals.SetValue(
            "json",
            DynamicCustomFunction.Create(new Func<object?, string>(SerializeJson)),
            true);

        var context = new TemplateContext(CreateBuiltins(), StringComparer.Ordinal)
        {
            EnableRelaxedFunctionAccess = false,
            EnableRelaxedIndexerAccess = false,
            EnableRelaxedMemberAccess = false,
            EnableRelaxedTargetAccess = false,
            LimitToString = MaximumOutputLength,
            LoopLimit = MaximumLoopCount,
            RecursiveLimit = MaximumRecursionDepth,
            RegexTimeOut = TimeSpan.FromSeconds(1),
            StrictVariables = true,
            TemplateLoader = null
        };
        context.PushGlobal(globals);
        context.PushOutput(new BoundedScriptOutput(MaximumOutputLength));

        try
        {
            var output = parsed.Render(context);

            if (output.Length > MaximumOutputLength)
            {
                throw new TemplateRenderException(
                    TemplateError.OutputLimitExceeded,
                    $"Template output exceeds the {MaximumOutputLength} character limit.");
            }

            return output;
        }
        catch (TemplateRenderException)
        {
            throw;
        }
        catch (ScriptRuntimeException exception)
        {
            var category = ClassifyRuntimeError(exception);
            var diagnostic = new TemplateDiagnostic(
                category,
                exception.OriginalMessage,
                exception.Span.Start.Line + 1,
                exception.Span.Start.Column + 1);

            throw new TemplateRenderException(
                category,
                "The template could not be rendered.",
                [diagnostic],
                exception);
        }
        finally
        {
            context.PopOutput();
            context.PopGlobal();
        }
    }

    private static object? ToScriptValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or DateTime or DateTimeOffset or TimeSpan or Guid => value,
            JsonNode node => ToScriptValueFromJson(node),
            IReadOnlyDictionary<string, object?> dictionary => ToScriptObject(dictionary),
            IDictionary dictionary => ToScriptObject(dictionary),
            IEnumerable enumerable => ToScriptArray(enumerable),
            _ => throw new TemplateRenderException(
                TemplateError.UnsupportedValue,
                $"Values of type '{value.GetType().Name}' cannot be exposed to templates.")
        };
    }

    private static ScriptObject ToScriptObject(IReadOnlyDictionary<string, object?> dictionary)
    {
        var result = new ScriptObject();

        foreach (var (name, value) in dictionary)
        {
            result.SetValue(name, ToScriptValue(value), true);
        }

        return result;
    }

    private static ScriptObject ToScriptObject(IDictionary dictionary)
    {
        var result = new ScriptObject();

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string name)
            {
                throw new TemplateRenderException(
                    TemplateError.UnsupportedValue,
                    "Template dictionaries must use string keys.");
            }

            result.SetValue(name, ToScriptValue(entry.Value), true);
        }

        return result;
    }

    private static ScriptArray ToScriptArray(IEnumerable values)
    {
        var result = new ScriptArray();

        foreach (var value in values)
        {
            result.Add(ToScriptValue(value));
        }

        return result;
    }

    private static object? ToScriptValueFromJson(JsonNode node)
    {
        return node switch
        {
            JsonObject value => ToScriptObject((IReadOnlyDictionary<string, object?>)value.ToDictionary(
                item => item.Key,
                item => (object?)item.Value,
                StringComparer.Ordinal)),
            JsonArray value => ToScriptArray(value),
            JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean,
            JsonValue value when value.TryGetValue<long>(out var integer) => integer,
            JsonValue value when value.TryGetValue<decimal>(out var number) => number,
            JsonValue value when value.TryGetValue<double>(out var floatingPoint) => floatingPoint,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => null
        };
    }

    private static string SerializeJson(object? value)
    {
        return JsonSerializer.Serialize(ToPlainValue(value), FlowControlJson.Options);
    }

    private static object? ToPlainValue(object? value)
    {
        return value switch
        {
            ScriptObject scriptObject => scriptObject.ToDictionary(
                item => item.Key,
                item => ToPlainValue(item.Value),
                StringComparer.Ordinal),
            ScriptArray scriptArray => scriptArray.Select(ToPlainValue).ToArray(),
            _ => value
        };
    }

    private static TemplateDiagnostic ToDiagnostic(LogMessage message) => new(
        TemplateError.InvalidTemplate,
        message.Message,
        message.Span.Start.Line + 1,
        message.Span.Start.Column + 1);

    private static TemplateError ClassifyRuntimeError(ScriptRuntimeException exception)
    {
        if (exception.OriginalMessage.Contains("output exceeds", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateError.OutputLimitExceeded;
        }

        if (exception.OriginalMessage.Contains("was not found", StringComparison.OrdinalIgnoreCase)
            || exception.OriginalMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || exception.OriginalMessage.Contains("cannot get", StringComparison.OrdinalIgnoreCase)
            || exception.OriginalMessage.Contains("cannot access target", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateError.MissingValue;
        }

        if (exception.OriginalMessage.Contains("limit", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateError.ExecutionLimitExceeded;
        }

        return TemplateError.RenderFailed;
    }

    private static ScriptObject CreateBuiltins()
    {
        var builtins = TemplateContext.GetDefaultBuiltinObject();
        builtins.Remove("date");
        builtins.Remove("include");
        builtins.Remove("include_join");
        builtins.Remove("object");

        return builtins;
    }

    private sealed class BoundedScriptOutput(int maximumLength) : IScriptOutput
    {
        private readonly StringBuilder _builder = new();

        public void Write(string text, int offset, int count)
        {
            if (_builder.Length + count > maximumLength)
            {
                throw new TemplateRenderException(
                    TemplateError.OutputLimitExceeded,
                    $"Template output exceeds the {maximumLength} character limit.");
            }

            _builder.Append(text, offset, count);
        }

        public ValueTask WriteAsync(
            string text,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(text, offset, count);

            return ValueTask.CompletedTask;
        }

        public override string ToString() => _builder.ToString();
    }
}
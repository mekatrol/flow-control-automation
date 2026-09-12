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

/// <summary>
/// Validates and renders Scriban templates against an explicitly constructed, restricted execution context.
/// </summary>
/// <remarks>
/// <para>
/// The service is deliberately stateless. Every render creates new globals, a new
/// <see cref="TemplateContext"/>, and a new output buffer. This makes the singleton registration safe for
/// concurrent callers and prevents values from one render becoming visible to another.
/// </para>
/// <para>
/// Templates can be supplied by configuration and therefore are treated as untrusted programs. The service
/// limits loops, recursion, regular-expression execution, and output size; disables relaxed member access;
/// removes built-ins that can expose objects or load other templates; and accepts only a controlled set of
/// dictionary value types. These restrictions keep rendering deterministic and bound its resource use.
/// </para>
/// <para>
/// A service-owned <c>json</c> function is always installed so templates can safely emit JSON values with
/// <c>{{ value | json }}</c>. Consequently, <c>json</c> is a reserved top-level name: allowing caller data to
/// replace the function would make template behavior depend on the supplied model and could bypass the JSON
/// escaping and formatting expected by a template author.
/// </para>
/// </remarks>
internal sealed class ScribanTemplateService : ITemplateService
{
    // These are policy limits, not Scriban defaults. They cap the amount of work an untrusted template can
    // request while remaining high enough for the controller payloads this service is designed to produce.
    private const int MaximumLoopCount = 2_000;
    private const int MaximumOutputLength = 1_000_000;
    private const int MaximumRecursionDepth = 64;

    /// <inheritdoc />
    /// <remarks>
    /// Validation only parses the source. It does not execute the template, so missing values and execution
    /// limit failures are intentionally reported later by <see cref="Render(string, IReadOnlyDictionary{string, object?})"/>.
    /// Scriban positions are converted to the one-based coordinates expected by API consumers.
    /// </remarks>
    public TemplateValidationResult Validate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var parsed = Template.Parse(template);

        return new([.. parsed.Messages.Select(ToDiagnostic)]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Dictionary input is converted recursively instead of imported through reflection. This creates a narrow
    /// trust boundary: templates receive scalars, JSON-like objects, and arrays, but cannot invoke arbitrary
    /// members on application objects accidentally stored in the dictionary.
    /// </remarks>
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

            // `json` is injected below as the service's JSON serializer. Reject a collision rather than silently
            // overwriting caller data or letting caller data shadow a function on which templates rely. The
            // comparison is ordinal because the template context and global object are also case-sensitive.
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

    /// <inheritdoc />
    /// <remarks>
    /// This overload intentionally uses Scriban's standard member renamer, which exposes .NET members using
    /// Scriban's conventional snake_case names (for example, <c>DeviceName</c> becomes <c>device_name</c>).
    /// Use the dictionary overload when values must pass through the restricted recursive type conversion.
    /// </remarks>
    public string Render(string template, object model)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(model);

        var globals = new ScriptObject();
        globals.Import(model, renamer: StandardMemberRenamer.Default);

        // Import can create a `json` global from a property, field, or dictionary entry. It must not collide with
        // the serializer installed by the common render path; rejecting it makes both public overloads obey the
        // same reserved-name contract.
        if (globals.ContainsKey("json"))
        {
            throw new TemplateRenderException(
                TemplateError.UnsupportedValue,
                "The template value name 'json' is reserved.");
        }

        return Render(template, globals);
    }

    /// <summary>Executes a parsed template using isolated globals and the service's safety policy.</summary>
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

        // A function (rather than Scriban's generic string conversion) preserves JSON types and applies the
        // application's shared serializer options and escaping rules.
        globals.SetValue(
            "json",
            DynamicCustomFunction.Create(new Func<object?, string>(SerializeJson)),
            true);

        // Ordinal lookup gives stable, culture-independent, case-sensitive variable semantics. Strict access
        // settings turn missing or inaccessible data into diagnostics instead of quietly producing empty output.
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

            // Template composition is intentionally unavailable: rendering must not read files or resolve
            // external templates, and all executable text must be present in the supplied template string.
            TemplateLoader = null
        };
        context.PushGlobal(globals);

        // LimitToString covers Scriban string conversions; the bounded sink independently caps the complete
        // rendered document as it is written, avoiding construction of an oversized intermediate result.
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
        // Preserve intentional policy failures (notably the output limit) and their stable application category.
        catch (TemplateRenderException)
        {
            throw;
        }
        // Scriban exception text is an implementation detail. Translate it once at this boundary so callers get
        // stable application categories plus the precise source position for troubleshooting.
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
            // Keep push/pop balanced even on failure. The context is local today, but explicit cleanup protects
            // correctness if context pooling is introduced later and documents Scriban's stack discipline.
            context.PopOutput();
            context.PopGlobal();
        }
    }

    /// <summary>
    /// Converts caller data into values that cannot expose an arbitrary .NET object surface to Scriban.
    /// </summary>
    /// <remarks>
    /// Strings are matched before <see cref="IEnumerable"/> so they remain scalar values rather than character
    /// arrays. Dictionaries are matched before other enumerables so they retain property/index lookup semantics.
    /// Unknown reference types are rejected instead of relying on reflection-based behavior that could change as
    /// those types evolve.
    /// </remarks>
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

    /// <summary>Recursively converts a strongly typed, string-keyed dictionary to Scriban globals.</summary>
    private static ScriptObject ToScriptObject(IReadOnlyDictionary<string, object?> dictionary)
    {
        var result = new ScriptObject();

        foreach (var (name, value) in dictionary)
        {
            result.SetValue(name, ToScriptValue(value), true);
        }

        return result;
    }

    /// <summary>Converts a non-generic dictionary while enforcing string keys.</summary>
    /// <remarks>
    /// Scriban object members are named strings. Rejecting other key types avoids ambiguous culture-sensitive
    /// string conversion and keeps access behavior consistent with JSON objects.
    /// </remarks>
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

    /// <summary>Materializes an enumerable as a recursively converted Scriban array.</summary>
    /// <remarks>Materialization gives the template a stable snapshot and avoids exposing iterator methods.</remarks>
    private static ScriptArray ToScriptArray(IEnumerable values)
    {
        var result = new ScriptArray();

        foreach (var value in values)
        {
            result.Add(ToScriptValue(value));
        }

        return result;
    }

    /// <summary>Maps a <see cref="JsonNode"/> tree to Scriban's object, array, and scalar types.</summary>
    /// <remarks>
    /// Integer and decimal representations are attempted before floating point to retain precision where the JSON
    /// value permits it. JSON null, or a value outside the supported JSON scalar forms, becomes a template null.
    /// </remarks>
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

    /// <summary>Serializes a template value with the application's canonical JSON options.</summary>
    private static string SerializeJson(object? value)
    {
        return JsonSerializer.Serialize(ToPlainValue(value), FlowControlJson.Options);
    }

    /// <summary>Removes Scriban container types before passing a value to <see cref="JsonSerializer"/>.</summary>
    /// <remarks>
    /// Converting to ordinary dictionaries and arrays ensures the output describes caller data, rather than
    /// Scriban runtime implementation details, and recursively supports objects nested inside arrays.
    /// </remarks>
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

    /// <summary>Converts a parser message to the service's stable, one-based diagnostic format.</summary>
    private static TemplateDiagnostic ToDiagnostic(LogMessage message) => new(
        TemplateError.InvalidTemplate,
        message.Message,
        message.Span.Start.Line + 1,
        message.Span.Start.Column + 1);

    /// <summary>Maps Scriban runtime failures to application-level error categories.</summary>
    /// <remarks>
    /// Scriban does not expose a structured error code for every failure used here, so classification necessarily
    /// uses its original message. Checks run from the most specific policy failure to the general fallback.
    /// </remarks>
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

    /// <summary>Builds the allowed built-in function set for each rendering context.</summary>
    /// <remarks>
    /// <c>include</c> and <c>include_join</c> are removed because external template loading is outside this
    /// service's closed-input model. <c>object</c> is removed to reduce runtime object introspection/manipulation.
    /// <c>date</c> is removed to avoid environment/time-dependent output; callers must supply any required time as
    /// explicit data. A fresh built-in object is used so removals never mutate state shared with another render.
    /// </remarks>
    private static ScriptObject CreateBuiltins()
    {
        var builtins = TemplateContext.GetDefaultBuiltinObject();
        builtins.Remove("date");
        builtins.Remove("include");
        builtins.Remove("include_join");
        builtins.Remove("object");

        return builtins;
    }

    /// <summary>An output sink that rejects a write before it would cross the configured character limit.</summary>
    /// <remarks>
    /// Enforcing the bound during writes limits memory growth. The post-render length check remains as defense in
    /// depth in case Scriban returns content through a path that does not use this sink in a future version.
    /// </remarks>
    private sealed class BoundedScriptOutput(int maximumLength) : IScriptOutput
    {
        private readonly StringBuilder _builder = new();

        /// <summary>Appends a text segment if the complete output remains within the limit.</summary>
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

        /// <summary>Provides Scriban's asynchronous output contract with the same synchronous bound check.</summary>
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

        /// <summary>Returns the accumulated rendered text.</summary>
        public override string ToString() => _builder.ToString();
    }
}
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

public sealed partial class ControllerTemplateValidator : IControllerTemplateValidator
{
    public ValidatedControllerTemplate Validate(
        ControllerTemplate template,
        bool allowBuiltInDefault = false)
    {
        var diagnostics = new List<ControllerDiagnostic>();
        if (template.SchemaVersion != 1)
        {
            Add(diagnostics, "unsupported_schema", "schemaVersion", "schemaVersion must be 1");
        }

        if (string.IsNullOrWhiteSpace(template.Id)
            || template.Id != template.Id.Trim()
            || !IdentifierRegex().IsMatch(template.Id))
        {
            Add(
                diagnostics,
                "invalid_id",
                "id",
                "id must be a lowercase identifier");
        }

        if (string.IsNullOrWhiteSpace(template.Name) || template.Name != template.Name.Trim())
        {
            Add(
                diagnostics,
                "invalid_name",
                "name",
                "name must be non-empty without surrounding whitespace");
        }

        if (!allowBuiltInDefault
            && (string.Equals(template.Id, BuiltInControllerTemplate.Id, StringComparison.Ordinal)
                || template.ReadOnly))
        {
            Add(
                diagnostics,
                "reserved_default",
                template.ReadOnly ? "readOnly" : "id",
                "custom templates cannot use the default id or readOnly state");
        }

        var pointTypes = template.Capabilities.PointTypes.ToHashSet();

        var directions = template.Capabilities.PointDirections.ToHashSet();

        var features = template.Capabilities.PointFeatures.ToHashSet();

        var connectors = template.Capabilities.ConnectorDataTypes.ToHashSet();

        var functions = ParseFunctions(template.Capabilities.FlowFunctions, diagnostics);

        var modes = template.Capabilities.ExecutionModes.ToHashSet();

        var runtime = template.Capabilities.RuntimeFeatures.ToHashSet();

        ValidateLimits(template.Limits, diagnostics);
        if (diagnostics.Count != 0)
        {
            throw new ControllerTemplateValidationException(diagnostics);
        }

        return new ValidatedControllerTemplate(
            template,
            pointTypes,
            directions,
            features,
            connectors,
            functions,
            modes,
            runtime);
    }

    private static HashSet<FlowFunctionKind> ParseFunctions(
        IReadOnlyList<FlowFunctionKind> values,
        List<ControllerDiagnostic> diagnostics)
    {
        const string path = "capabilities.flowFunctions";

        RequireValues(values, path, diagnostics);

        var result = new HashSet<FlowFunctionKind>();

        for (var index = 0; index < values.Count; index++)
        {
            if (!result.Add(values[index]))
            {
                Add(
                    diagnostics,
                    "duplicate_capability",
                    $"{path}[{index}]",
                    "flow function is duplicated");
            }
            else if (!FlowNodeRegistry.Functions.Contains(values[index]))
            {
                Add(
                    diagnostics,
                    "unsupported_capability",
                    $"{path}[{index}]",
                    $"unsupported flow function \"{values[index]}\"");
            }
        }

        return result;
    }

    private static void RequireValues<T>(
        IReadOnlyList<T> values,
        string path,
        List<ControllerDiagnostic> diagnostics)
    {
        if (values.Count == 0)
        {
            Add(diagnostics, "empty_capability", path, "at least one capability is required");
        }
    }

    private static void ValidateLimits(
        ControllerLimits limits,
        List<ControllerDiagnostic> diagnostics)
    {
        Positive(limits.MaxFlows, "limits.maxFlows", diagnostics);
        Positive(limits.MaxNodesPerFlow, "limits.maxNodesPerFlow", diagnostics);
        Positive(limits.MaxConnectionsPerFlow, "limits.maxConnectionsPerFlow", diagnostics);
        Positive(
            limits.MinimumIntervalMilliseconds,
            "limits.minimumIntervalMilliseconds",
            diagnostics);
    }

    private static void Positive(
        int? value,
        string path,
        List<ControllerDiagnostic> diagnostics)
    {
        if (value is <= 0)
        {
            Add(diagnostics, "invalid_limit", path, "limit must be positive or null");
        }
    }

    private static void Add(
        List<ControllerDiagnostic> diagnostics,
        string code,
        string path,
        string message) =>
        diagnostics.Add(new ControllerDiagnostic(code, path, message));

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();
}
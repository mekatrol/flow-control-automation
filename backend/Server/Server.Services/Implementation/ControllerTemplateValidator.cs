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

        var pointTypes = Parse(
            template.Capabilities.PointTypes,
            "capabilities.pointTypes",
            ParsePointType,
            diagnostics);
        var directions = Parse(
            template.Capabilities.PointDirections,
            "capabilities.pointDirections",
            ParsePointDirection,
            diagnostics);
        var features = Parse(
            template.Capabilities.PointFeatures,
            "capabilities.pointFeatures",
            ParsePointFeature,
            diagnostics);
        var connectors = Parse(
            template.Capabilities.ConnectorDataTypes,
            "capabilities.connectorDataTypes",
            ParseConnectorDataType,
            diagnostics);
        var functions = ParseFunctions(template.Capabilities.FlowFunctions, diagnostics);
        var modes = Parse(
            template.Capabilities.ExecutionModes,
            "capabilities.executionModes",
            ParseExecutionMode,
            diagnostics);
        var runtime = Parse(
            template.Capabilities.RuntimeFeatures,
            "capabilities.runtimeFeatures",
            ParseRuntimeFeature,
            diagnostics);

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

    private static IReadOnlySet<T> Parse<T>(
        IReadOnlyList<string> values,
        string path,
        Func<string, T?> parser,
        List<ControllerDiagnostic> diagnostics)
        where T : struct, Enum
    {
        RequireValues(values, path, diagnostics);
        var result = new HashSet<T>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < values.Count; index++)
        {
            var itemPath = $"{path}[{index}]";
            if (!seen.Add(values[index]))
            {
                Add(diagnostics, "duplicate_capability", itemPath, "capability is duplicated");
                continue;
            }

            var parsed = parser(values[index]);
            if (parsed is null)
            {
                Add(
                    diagnostics,
                    "unsupported_capability",
                    itemPath,
                    $"unsupported capability \"{values[index]}\"");
                continue;
            }

            result.Add(parsed.Value);
        }

        return result;
    }

    private static IReadOnlySet<string> ParseFunctions(
        IReadOnlyList<string> values,
        List<ControllerDiagnostic> diagnostics)
    {
        const string path = "capabilities.flowFunctions";
        RequireValues(values, path, diagnostics);
        var result = new HashSet<string>(StringComparer.Ordinal);
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

    private static void RequireValues(
        IReadOnlyList<string> values,
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

    private static PointValueType? ParsePointType(string value) => value switch
    {
        "analog" => PointValueType.Analog,
        "digital" => PointValueType.Digital,
        "multi_state" => PointValueType.MultiState,
        "integer" => PointValueType.Integer,
        "text" => PointValueType.Text,
        _ => null,
    };

    private static DataDirection? ParsePointDirection(string value) => value switch
    {
        "input" => DataDirection.Input,
        "output" => DataDirection.Output,
        "input_output" => DataDirection.InputOutput,
        "value" => DataDirection.Value,
        _ => null,
    };

    private static ControllerPointFeature? ParsePointFeature(string value) => value switch
    {
        "read" => ControllerPointFeature.Read,
        "command" => ControllerPointFeature.Command,
        "retain" => ControllerPointFeature.Retain,
        "override" => ControllerPointFeature.Override,
        "relinquish" => ControllerPointFeature.Relinquish,
        "quality" => ControllerPointFeature.Quality,
        "alarms" => ControllerPointFeature.Alarms,
        "trends" => ControllerPointFeature.Trends,
        _ => null,
    };

    private static ConnectorDataType? ParseConnectorDataType(string value) => value switch
    {
        "any" => ConnectorDataType.Any,
        "boolean" => ConnectorDataType.Boolean,
        "event" => ConnectorDataType.Event,
        "number" => ConnectorDataType.Number,
        "string" => ConnectorDataType.String,
        _ => null,
    };

    private static ExecutionMode? ParseExecutionMode(string value) => value switch
    {
        "event" => ExecutionMode.Event,
        "interval" => ExecutionMode.Interval,
        _ => null,
    };

    private static ControllerRuntimeFeature? ParseRuntimeFeature(string value) => value switch
    {
        "virtual_points" => ControllerRuntimeFeature.VirtualPoints,
        "bound_points" => ControllerRuntimeFeature.BoundPoints,
        "command_arbitration" => ControllerRuntimeFeature.CommandArbitration,
        "quality_propagation" => ControllerRuntimeFeature.QualityPropagation,
        _ => null,
    };

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();
}
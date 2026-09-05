using Server.Common;
using Server.Common.Contracts;
using Server.Common.Models;
using Server.Common.Services;
using Server.Common.Types;
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

        var functions = ParseFunctions(template.Capabilities.FlowFunctions, diagnostics);

        var pointTypes = ParseEnumCapabilities(
                    template.Capabilities.PointTypes,
                    "capabilities.pointTypes",
                    diagnostics);

        var directions = ParseEnumCapabilities(
            template.Capabilities.PointDirections,
            "capabilities.pointDirections",
            diagnostics);

        var features = ParseEnumCapabilities(
            template.Capabilities.PointFeatures,
            "capabilities.pointFeatures",
            diagnostics);

        var connectors = ParseEnumCapabilities(
            template.Capabilities.ConnectorDataTypes,
            "capabilities.connectorDataTypes",
            diagnostics);

        var modes = ParseEnumCapabilities(
            template.Capabilities.ExecutionModes,
            "capabilities.executionModes",
            diagnostics);

        var runtime = ParseEnumCapabilities(
            template.Capabilities.RuntimeFeatures,
            "capabilities.runtimeFeatures",
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

    private static HashSet<T> ParseEnumCapabilities<T>(
    IReadOnlyList<T> values,
    string path,
    List<ControllerDiagnostic> diagnostics)
    where T : struct, Enum
    {
        RequireValues(values, path, diagnostics);

        var result = new HashSet<T>();

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];

            if (!result.Add(value))
            {
                Add(
                    diagnostics,
                    "duplicate_capability",
                    $"{path}[{index}]",
                    "capability is duplicated");

                continue;
            }

            if (!Enum.IsDefined(value))
            {
                Add(
                    diagnostics,
                    "unsupported_capability",
                    $"{path}[{index}]",
                    $"unsupported capability \"{value}\"");
            }
        }

        return result;
    }

    private static HashSet<FlowFunctionType> ParseFunctions(
        IReadOnlyList<FlowFunctionType> values,
        List<ControllerDiagnostic> diagnostics)
    {
        const string path = "capabilities.flowFunctions";

        RequireValues(values, path, diagnostics);

        var result = new HashSet<FlowFunctionType>();

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
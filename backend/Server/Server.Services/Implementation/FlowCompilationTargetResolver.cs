using Server.Services.Contracts;

namespace Server.Services.Implementation;

public sealed class FlowCompilationTargetResolver(
    IControllerTemplateStore controllerTemplates,
    IControllerTemplateValidator controllerTemplateValidator,
    IPointDefinitionStore pointDefinitions) : IFlowCompilationTargetResolver
{
    public async Task<FlowCompilationTarget> ResolveAsync(
        ExecutableFlowSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        ControllerTemplate template;
        try
        {
            template = await controllerTemplates.GetAsync(
                source.ControllerTemplateId,
                cancellationToken);
        }
        catch (ControllerTemplateNotFoundException)
        {
            throw Failure(
                "target_mismatch",
                "/controllerTemplateId",
                $"Controller template \"{source.ControllerTemplateId}\" was not found.");
        }

        if (template.Revision < 0 || (uint)template.Revision != source.ControllerTemplateRevision)
        {
            throw Failure(
                "target_mismatch",
                "/controllerTemplateRevision",
                $"Expected controller template revision {source.ControllerTemplateRevision}, "
                    + $"but resolved revision {template.Revision}.");
        }

        var validated = controllerTemplateValidator.Validate(
            template,
            allowBuiltInDefault: string.Equals(
                template.Id,
                BuiltInControllerTemplate.Id,
                StringComparison.Ordinal));
        ValidateCapabilities(source, validated);
        ValidateLimits(source, template.Limits);

        var allPoints = await pointDefinitions.ListPointsAsync(cancellationToken);
        var pointsById = allPoints.ToDictionary(point => point.Id, StringComparer.Ordinal);
        var resolvedPoints = new List<Point>();
        foreach (var reference in PointReferences(source))
        {
            if (!pointsById.TryGetValue(reference.PointId, out var point))
            {
                throw Failure(
                    "missing_point",
                    $"/points/{Escape(reference.PointId)}",
                    $"Point \"{reference.PointId}\" was not found.");
            }

            ValidatePoint(reference, point);
            resolvedPoints.Add(point);
        }

        return new FlowCompilationTarget
        {
            ControllerTemplate = validated,
            Points = resolvedPoints
        };
    }

    private static void ValidateCapabilities(
        ExecutableFlowSource source,
        ValidatedControllerTemplate template)
    {
        if (!ControllerCapabilitiesSupport.SupportsConnector(template, ConnectorDataType.Boolean)
            || !template.PointTypes.Contains(PointValueType.Digital))
        {
            throw Failure(
                "unsupported_target_capability",
                "/controllerTemplateId",
                "The target must support Boolean connectors and digital points.");
        }

        var functions = source.Nodes
            .Select(node => RequiredFunction(node.Kind))
            .Where(function => function is not null)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        foreach (var function in functions)
        {
            if (!ControllerCapabilitiesSupport.SupportsFunction(template, function!))
            {
                throw Failure(
                    "unsupported_target_capability",
                    "/controllerTemplateId",
                    $"The target does not support flow function \"{function}\".");
            }
        }
    }

    private static void ValidateLimits(ExecutableFlowSource source, ControllerLimits limits)
    {
        if (limits.MaxNodesPerFlow is int maxNodes && source.Nodes.Count > maxNodes)
        {
            throw Failure(
                "limit_exceeded",
                "/nodes",
                $"The target permits at most {maxNodes} nodes per flow.");
        }

        if (limits.MaxConnectionsPerFlow is int maxConnections
            && source.Connections.Count > maxConnections)
        {
            throw Failure(
                "limit_exceeded",
                "/connections",
                $"The target permits at most {maxConnections} connections per flow.");
        }
    }

    private static IReadOnlyList<PointReference> PointReferences(ExecutableFlowSource source) =>
        source.Nodes
            .Select(node => new { Node = node, PointId = PointId(node) })
            .Where(item => item.PointId is not null)
            .Select(item => new PointReference(
                item.PointId!,
                item.Node.Kind is "digitalInput" or "analogInput",
                item.Node.Kind.StartsWith("analog", StringComparison.Ordinal)))
            .Distinct()
            .OrderBy(reference => reference.PointId, StringComparer.Ordinal)
            .ThenBy(reference => reference.IsInput ? 0 : 1)
            .ToArray();

    private static string? PointId(ExecutableFlowNode node) =>
        node.Kind is "digitalInput" or "digitalOutput" or "analogInput" or "analogOutput"
        && node.Configuration.TryGetValue("pointId", out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static void ValidatePoint(PointReference reference, Point point)
    {
        var valid = point.Enabled
            && string.Equals(point.ValueType, reference.IsAnalog ? "analog" : "digital", StringComparison.Ordinal)
            && (reference.IsInput
                ? point.Readable && string.Equals(point.Direction, "input", StringComparison.Ordinal)
                : point.Commandable && string.Equals(point.Direction, "output", StringComparison.Ordinal));
        if (!valid)
        {
            throw Failure(
                "point_direction_mismatch",
                $"/points/{Escape(reference.PointId)}",
                $"Point \"{reference.PointId}\" is not a compatible enabled {(reference.IsAnalog ? "analog" : "digital")} "
                    + (reference.IsInput ? "input." : "output."));
        }
    }

    private static string? RequiredFunction(string kind) => kind switch
    {
        "digitalInput" => "read-point",
        "analogInput" => "read-point",
        "digitalOutput" => "write-point",
        "analogOutput" => "write-point",
        "not" => "not",
        "and" => "and",
        "or" => "or",
        _ => null
    };

    private static FlowCompilationException Failure(string code, string path, string message) =>
        new([new FlowCompilationDiagnostic(code, path, message)]);

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal)
        .Replace("/", "~1", StringComparison.Ordinal);

    private sealed record PointReference(string PointId, bool IsInput, bool IsAnalog);
}

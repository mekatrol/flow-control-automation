using Server.Common;
using Server.Common.Contracts;
using Server.Common.Models;
using Server.Common.Types;
using Server.Compiler.Contracts;

namespace Server.Compiler.Services.Implementation;

internal sealed class FlowCompilationTargetResolver(
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
            throw Failure(FlowCompilationDiagnosticCode.ControllerTemplateNotFound, "/controllerTemplateId", source.ControllerTemplateId);
        }

        if (template.Revision < 0 || (uint)template.Revision != source.ControllerTemplateRevision)
        {
            throw Failure(
                FlowCompilationDiagnosticCode.ControllerTemplateRevisionMismatch,
                "/controllerTemplateRevision",
                source.ControllerTemplateRevision,
                template.Revision
            );
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
        foreach (var declaration in source.VirtualPointDefinitions)
        {
            pointsById.TryAdd(declaration.Key, VirtualPoint(declaration));
        }
        var resolvedPoints = new List<AutomationPoint>();
        foreach (var reference in PointReferences(source))
        {
            if (!pointsById.TryGetValue(reference.PointId, out var point))
            {
                throw Failure(FlowCompilationDiagnosticCode.MissingPoint, $"/points/{Escape(reference.PointId)}", reference.PointId);
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

    private static VirtualAutomationPoint VirtualPoint(VirtualPointDefinition declaration) => new()
    {
        Id = declaration.Key,
        Name = declaration.Key,
        Enabled = true,
        Direction = DataDirectionType.Value,
        ValueType = declaration.ValueType,
        Units = declaration.Units,
        Readable = declaration.Readable,
        Commandable = declaration.Commandable,
        Persistence = declaration.Persistence == VirtualPointPersistenceType.Retained ? "retained" : "volatile",
        RelinquishDefault = declaration.RelinquishDefault is { } value
            ? System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText()) : null,
        Revision = 1
    };

    private static void ValidateCapabilities(
        ExecutableFlowSource source,
        ValidatedControllerTemplate template)
    {
        if (!ControllerCapabilitiesSupport.SupportsConnector(template, ConnectorDataType.Boolean)
            || !template.PointTypes.Contains(AutomationPointValueType.Digital))
        {
            throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetConnectorCapability, "/controllerTemplateId");
        }

        var functions = source.Nodes
            .Select(node => RequiredFunction(node.NodeType))
            .OfType<FlowFunctionType>()
            .Distinct()
            .Order();

        foreach (var function in functions)
        {
            if (!ControllerCapabilitiesSupport.SupportsFunction(template, function))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetFunction, "/controllerTemplateId", function);
            }
        }

        if (source.VirtualPointDefinitions.Count == 0)
        {
            return;
        }

        if (!template.RuntimeFeatures.Contains(ControllerRuntimeFeatureType.VirtualPoints))
        {
            throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetPointCapability, "/virtualPointDeclarations", ControllerRuntimeFeatureType.VirtualPoints);
        }

        foreach (var declaration in source.VirtualPointDefinitions)
        {
            if (!template.PointTypes.Contains(declaration.ValueType))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetPointCapability, $"/virtualPointDeclarations/{Escape(declaration.Key)}/valueType", declaration.ValueType);
            }

            if (declaration.Readable && !template.PointFeatures.Contains(ControllerPointFeatureType.Read))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetPointCapability, $"/virtualPointDeclarations/{Escape(declaration.Key)}/readable", ControllerPointFeatureType.Read);
            }

            if (declaration.Commandable && !template.PointFeatures.Contains(ControllerPointFeatureType.Command))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetPointCapability, $"/virtualPointDeclarations/{Escape(declaration.Key)}/commandable", ControllerPointFeatureType.Command);
            }

            if (declaration.Persistence == VirtualPointPersistenceType.Retained
                && !template.PointFeatures.Contains(ControllerPointFeatureType.Retain))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedTargetPointCapability, $"/virtualPointDeclarations/{Escape(declaration.Key)}/persistence", ControllerPointFeatureType.Retain);
            }
        }
    }

    private static void ValidateLimits(ExecutableFlowSource source, ControllerLimits limits)
    {
        if (limits.MaxNodesPerFlow is int maxNodes && source.Nodes.Count > maxNodes)
        {
            throw Failure(FlowCompilationDiagnosticCode.TargetNodeLimitExceeded, "/nodes", maxNodes);
        }

        if (limits.MaxConnectionsPerFlow is int maxConnections
            && source.Connections.Count > maxConnections)
        {
            throw Failure(FlowCompilationDiagnosticCode.TargetConnectionLimitExceeded, "/connections", maxConnections);
        }
    }

    private static IReadOnlyList<PointReference> PointReferences(ExecutableFlowSource source) =>
        [.. source.Nodes
            .Select(node => new { Node = node, PointId = PointId(node) })
            .Where(item => item.PointId is not null)
            .Select(item => new PointReference(
                item.PointId!,
                item.Node.NodeType is FlowNodeType.DigitalInput or FlowNodeType.AnalogInput,
                item.Node.NodeType.ToString().StartsWith("Analog", StringComparison.Ordinal)))
            .Distinct()
            .OrderBy(reference => reference.PointId, StringComparer.Ordinal)
            .ThenBy(reference => reference.IsInput ? 0 : 1)];

    private static string? PointId(ExecutableFlowNode node) =>
        node.NodeType is FlowNodeType.DigitalInput or FlowNodeType.DigitalOutput or FlowNodeType.AnalogInput or FlowNodeType.AnalogOutput &&
        node.Configuration.TryGetValue("pointId", out var value) &&
        value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static void ValidatePoint(PointReference reference, AutomationPoint point)
    {
        var virtualValue = point.PointSourceType == PointSourceType.Virtual
            && point.Direction == DataDirectionType.Value;

        var valid = point.Enabled
            && point.ValueType == (reference.IsAnalog ? AutomationPointValueType.Analog : AutomationPointValueType.Digital)
            && (reference.IsInput
                ? point.Readable && (point.Direction == DataDirectionType.Input || virtualValue)
                : point.Commandable && (point.Direction == DataDirectionType.Output || virtualValue));

        if (!valid)
        {
            throw Failure(
                FlowCompilationDiagnosticCode.PointDirectionMismatch,
                $"/points/{Escape(reference.PointId)}",
                reference.PointId,
                reference.IsAnalog ? "analog" : "digital",
                reference.IsInput ? "input" : "output"
            );
        }
    }

    private static FlowFunctionType? RequiredFunction(FlowNodeType kind) => kind switch
    {
        FlowNodeType.DigitalInput => FlowFunctionType.ReadPoint,
        FlowNodeType.AnalogInput => FlowFunctionType.ReadPoint,
        FlowNodeType.DigitalOutput => FlowFunctionType.WritePoint,
        FlowNodeType.AnalogOutput => FlowFunctionType.WritePoint,
        FlowNodeType.Not => FlowFunctionType.Not,
        FlowNodeType.And => FlowFunctionType.And,
        FlowNodeType.Or => FlowFunctionType.Or,
        _ => null
    };

    private static FlowCompilationException Failure(
        FlowCompilationDiagnosticCode code,
        string path,
        params object?[] arguments) =>
        new([FlowCompilationDiagnostics.Create(code, path, arguments)]);

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal)
        .Replace("/", "~1", StringComparison.Ordinal);

    private sealed record PointReference(string PointId, bool IsInput, bool IsAnalog);
}
using Server.Common;
using Server.Common.Contracts;
using Server.Compiler.Contracts;
using Server.Compiler.Services;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class FlowDeploymentService(
    IFlowCompilationTargetResolver targetResolver,
    IFlowCompiler compiler,
    IFlowRuntimeService runtime) : IFlowDeploymentService
{
    public async Task<RuntimeSnapshot> DeployAsync(
        Flow flow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var source = ToExecutableSource(flow);

        var target = await targetResolver.ResolveAsync(
            source,
            cancellationToken);

        var compilation = compiler.Compile(new FlowCompilationRequest
        {
            Source = source,
            Target = target
        });

        var inputPointIds = source.Nodes
            .Where(node => node.Kind == FlowNodeKind.DigitalInput)
            .Select(node => node.Configuration["pointId"].GetString()!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return await runtime.DeployAsync(
            flow,
            compilation,
            inputPointIds,
            TimeSpan.FromMilliseconds(100),
            cancellationToken);
    }

    internal static ExecutableFlowSource ToExecutableSource(
        Flow flow,
        string? controllerTemplateId = null,
        int? controllerTemplateRevision = null,
        IReadOnlyDictionary<string, string>? physicalPointBindings = null)
    {
        var graph = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                flow.Nodes,
                flow.Connections
            },
            FlowControlJson.Options);

        var revision = BinaryPrimitives.ReadUInt32LittleEndian(
            SHA256.HashData(graph));

        if (revision == 0)
        {
            revision = 1;
        }

        return new ExecutableFlowSource
        {
            Id = flow.Id,
            Revision = revision,

            ControllerTemplateId = controllerTemplateId ?? BuiltInControllerTemplate.Id,
            ControllerTemplateRevision =
                checked((uint)(controllerTemplateRevision ?? BuiltInControllerTemplate.Default.Revision)),

            Execution = new ExecutableFlowExecution
            {
                // The artifact is a deterministic single-scan program.
                // The server host owns the 100 ms interval used to invoke it.
                Mode = FlowExecutionMode.Manual,
                IntervalMs = 0,
                InputQualityPolicy = flow.Nodes.Any(
                    node => node.Kind == FlowNodeKind.QualityGood)
                        ? InputQualityPolicy.Propagate
                        : InputQualityPolicy.RequireGood
            },

            Nodes =
            [
                .. flow.Nodes
                    .Where(node => !node.Kind.IsVirtual() || flow.Connections.Any(
                        connection => connection.Start.NodeId == node.Id))
                    .Select(node => new ExecutableFlowNode
                {
                    Id = node.Id,
                    Kind = node.Kind.ExecutableKind(),
                    Configuration = node.Kind.IsVirtual()
                        ? VirtualPointConfiguration(node)
                        : BindConfiguration(node, physicalPointBindings),
                    Label = node.Label,
                    X = node.X,
                    Y = node.Y,
                    ZOrder = node.ZOrder,
                    GroupId = node.GroupId
                })
            ],

            Connections =
            [
                .. flow.Connections.Select(connection =>
                    new ExecutableFlowConnection(
                        new ExecutableFlowEndpoint(
                            connection.Start.NodeId,
                            connection.Start.ConnectorId),
                        new ExecutableFlowEndpoint(
                            connection.End.NodeId,
                            connection.End.ConnectorId)))
            ],

            VirtualPointDeclarations = VirtualPointNodes.Declarations(flow.Nodes)
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> BindConfiguration(
        FlowNode node,
        IReadOnlyDictionary<string, string>? bindings)
    {
        if (bindings is null
            || !node.Configuration.TryGetValue("pointId", out var pointId)
            || pointId.ValueKind != JsonValueKind.String
            || pointId.GetString() is not { } role
            || !bindings.TryGetValue(role, out var resolvedPointId))
        {
            return node.Configuration;
        }

        var result = new Dictionary<string, JsonElement>(node.Configuration, StringComparer.Ordinal)
        {
            ["pointId"] = JsonSerializer.SerializeToElement(resolvedPointId)
        };
        return result;
    }

    private static Dictionary<string, JsonElement> VirtualPointConfiguration(FlowNode node) =>
        node.Configuration.TryGetValue("pointId", out var pointId)
            ? new Dictionary<string, JsonElement> { ["pointId"] = pointId }
            : new Dictionary<string, JsonElement>();
}
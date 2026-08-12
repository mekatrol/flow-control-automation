using Server.Services.Contracts;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class FlowDeploymentService(
    IFlowCompilationTargetResolver targetResolver,
    IFlowCompiler compiler,
    IFlowRuntimeService runtime) : IFlowDeploymentService
{
    public async Task<RuntimeSnapshot> DeployAsync(Flow flow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);
        var source = ToExecutableSource(flow);
        var target = await targetResolver.ResolveAsync(source, cancellationToken);
        var compilation = compiler.Compile(new FlowCompilationRequest
        {
            Source = source,
            Target = target
        });
        var inputPointIds = source.Nodes
            .Where(node => node.Kind == "digitalInput")
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

    private static ExecutableFlowSource ToExecutableSource(Flow flow)
    {
        var unsupported = flow.Nodes.FirstOrDefault(node => node.Kind is not (
            "digitalInput" or "digitalConstant" or "not" or "and" or "or" or "memory" or "digitalOutput"));
        if (unsupported is not null)
        {
            throw new FlowCompilationException([
                new FlowCompilationDiagnostic(
                    "unsupported_node",
                    $"/nodes/{unsupported.Id}",
                    $"Node kind \"{unsupported.Kind}\" is not supported by the current IL compiler.")]);
        }

        var graph = JsonSerializer.SerializeToUtf8Bytes(
            new { flow.Nodes, flow.Connections },
            FlowControlJson.Options);
        var revision = BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(graph));
        if (revision == 0) revision = 1;
        return new ExecutableFlowSource
        {
            Id = flow.Id,
            Revision = revision,
            ControllerTemplateId = BuiltInControllerTemplate.Id,
            ControllerTemplateRevision = checked((uint)BuiltInControllerTemplate.Default.Revision),
            Execution = new ExecutableFlowExecution
            {
                // The artifact is a deterministic single-scan program. The server host
                // owns the 100 ms interval used to invoke that program.
                Mode = "manual",
                IntervalMs = 0,
                InputQualityPolicy = "require_good"
            },
            Nodes = flow.Nodes.Select(node => new ExecutableFlowNode
            {
                Id = node.Id,
                Kind = node.Kind,
                Configuration = node.Configuration
            }).ToArray(),
            Connections = flow.Connections.Select(connection => new ExecutableFlowConnection(
                new ExecutableFlowEndpoint(connection.Start.NodeId, connection.Start.ConnectorId),
                new ExecutableFlowEndpoint(connection.End.NodeId, connection.End.ConnectorId))).ToArray()
        };
    }
}

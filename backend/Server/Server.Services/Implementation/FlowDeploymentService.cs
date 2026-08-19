using Server.Common;
using Server.Common.Contracts;
using Server.Common.Services;
using Server.Compiler;
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

    private static ExecutableFlowSource ToExecutableSource(Flow flow)
    {
        var unsupported = flow.Nodes.FirstOrDefault(node => node.Kind is not (
            FlowNodeKind.DigitalInput or
            FlowNodeKind.DigitalConstant or
            FlowNodeKind.Not or
            FlowNodeKind.And or
            FlowNodeKind.Or or
            FlowNodeKind.Memory or
            FlowNodeKind.DigitalOutput));

        if (unsupported is not null)
        {
            throw Failure(
                FlowCompilationDiagnosticCode.UnsupportedNode,
                $"/nodes/{Escape(unsupported.Id)}",
                unsupported.Kind);
        }

        var graph = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                flow.Nodes,
                flow.Connections,
                flow.Interface
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

            ControllerTemplateId = BuiltInControllerTemplate.Id,
            ControllerTemplateRevision =
                checked((uint)BuiltInControllerTemplate.Default.Revision),

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
                .. flow.Nodes.Select(node => new ExecutableFlowNode
                {
                    Id = node.Id,
                    Kind = node.Kind,
                    Configuration = node.Configuration,
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

            Interface = flow.Interface
        };
    }

    private static FlowCompilationException Failure(
        FlowCompilationDiagnosticCode code,
        string path,
        params object?[] arguments) =>
        new([
            FlowCompilationDiagnostics.Create(
                code,
                path,
                arguments)
        ]);

    private static string Escape(string value) =>
        value
            .Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
}
using Server.Services.Contracts;

namespace Server.Services;

/// <summary>
/// Compiles an editable flow for an already-resolved controller target.
/// Implementations must be deterministic and must not persist flows or perform transport I/O.
/// </summary>
public interface IFlowCompiler
{
    FlowCompilationResult Compile(FlowCompilationRequest request);
}

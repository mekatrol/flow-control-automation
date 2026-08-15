using Server.Services.Contracts;

namespace Server.Services;

/// <summary>
/// Compiles an editable flow for an already-resolved controller target.
/// Implementations must be deterministic and must not persist flows or perform transport I/O.
/// </summary>
public interface IFlowCompiler
{
    /// <summary>Validates and deterministically compiles one resolved flow snapshot into the current portable Flow IL version.</summary>
    /// <param name="request">The immutable source and fully resolved target; IDs and revisions must agree and all dependencies must already be validated.</param>
    /// <returns>The bounded artifact, normalized executable metadata, and deterministic diagnostics; compilation performs no persistence or transport I/O.</returns>
    /// <exception cref="FlowCompilationException">Thrown when the graph, target, dependency, or artifact limit prevents compilation.</exception>
    FlowCompilationResult Compile(FlowCompilationRequest request);
}
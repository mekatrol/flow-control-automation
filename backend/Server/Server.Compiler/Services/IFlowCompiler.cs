using Server.Common.Contracts;

namespace Server.Compiler.Services;

/// <summary>
/// Compiles an editable flow for an already-resolved controller target.
/// Implementations must be deterministic and must not persist flows or perform transport I/O.
/// </summary>
public interface IFlowCompiler
{
    /// <summary>
    /// Validates and deterministically compiles one resolved flow snapshot into the
    /// current portable Flow IL version.
    /// </summary>
    /// <param name="request">
    /// The immutable source and fully resolved target. IDs and revisions must agree,
    /// and all dependencies must already be validated.
    /// </param>
    /// <returns>
    /// The compiled Flow IL artifact together with its normalized executable metadata.
    /// </returns>
    /// <exception cref="FlowCompilationException">
    /// Thrown when the graph, target, dependency, or artifact limits prevent compilation.
    /// </exception>
    FlowCompilationResult Compile(FlowCompilationRequest request);

    /// <summary>
    /// Writes a compiled Flow IL artifact to a binary file without modifying or
    /// recompiling the artifact.
    /// </summary>
    /// <param name="compilation">
    /// The completed compilation result containing the Flow IL artifact to write.
    /// </param>
    /// <param name="path">
    /// The destination file path. An existing file at the path is overwritten.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="compilation"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is null, empty, or consists only of
    /// white-space characters.
    /// </exception>
    void WriteBinary(
        FlowCompilationResult compilation,
        string path);

    /// <summary>
    /// Writes a compiled Flow IL artifact as an Intel HEX file without modifying or
    /// recompiling the artifact.
    /// </summary>
    /// <param name="compilation">
    /// The completed compilation result containing the Flow IL artifact to encode.
    /// </param>
    /// <param name="path">
    /// The destination file path. An existing file at the path is overwritten.
    /// </param>
    /// <param name="baseAddress">
    /// The address at which the first artifact byte is placed in the Intel HEX
    /// address space.
    /// </param>
    /// <param name="bytesPerRecord">
    /// The maximum number of artifact bytes written to each Intel HEX data record.
    /// Must be between 1 and 255 inclusive.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="compilation"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is null, empty, or consists only of
    /// white-space characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bytesPerRecord"/> is outside the range 1 through 255.
    /// </exception>
    void WriteIntelHex(
        FlowCompilationResult compilation,
        string path,
        uint baseAddress = 0,
        int bytesPerRecord = 16);
}
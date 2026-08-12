using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowDecompiler
{
    FlowDecompilationResult Decompile(ReadOnlyMemory<byte> artifact, string? name = null);
}

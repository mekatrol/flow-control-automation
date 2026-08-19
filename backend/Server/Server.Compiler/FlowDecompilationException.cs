using Server.Common.Contracts;

namespace Server.Compiler;

public sealed class FlowDecompilationException(FlowCompilationDiagnostic diagnostic)
    : Exception($"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")
{
    public FlowCompilationDiagnostic Diagnostic { get; } = diagnostic;
}
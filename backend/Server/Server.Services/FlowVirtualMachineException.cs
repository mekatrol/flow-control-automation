namespace Server.Services;

public sealed class FlowVirtualMachineException(int code, string path)
    : Exception($"Portable VM failure {code} at {path}")
{
    public int Code { get; } = code;
    public string Path { get; } = path;
}
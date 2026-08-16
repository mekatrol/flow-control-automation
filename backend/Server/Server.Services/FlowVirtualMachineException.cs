namespace Server.Services;

public sealed class FlowVirtualMachineException(FlowVirtualMachineErrorCode code, string path)
    : Exception($"Portable VM failure {code} at {path}")
{
    public FlowVirtualMachineErrorCode Code { get; } = code;
    
    public string Path { get; } = path;
}
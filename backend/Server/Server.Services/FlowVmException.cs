namespace Server.Services;

public sealed class FlowVmException(FlowVmErrorCode code, string path)
    : Exception($"Portable VM failure {code} at {path}")
{
    public FlowVmErrorCode Code { get; } = code;
    
    public string Path { get; } = path;
}
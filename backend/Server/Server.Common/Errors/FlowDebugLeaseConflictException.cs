namespace Server.Common.Errors;

public sealed class FlowDebugLeaseConflictException(string flowId, Exception? innerException = null)
    : Exception($"Flow '{flowId}' is already being debugged.", innerException)
{
    public string Code => "flow_already_being_debugged";
}
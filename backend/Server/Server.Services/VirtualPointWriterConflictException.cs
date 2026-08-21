namespace Server.Services;

public sealed class VirtualPointWriterConflictException(
    string executionInstanceId,
    string pointKey,
    string writerFlowId) : Exception(
        $"Virtual point '{pointKey}' on execution instance '{executionInstanceId}' is already written by flow '{writerFlowId}'.")
{
    public string ExecutionInstanceId { get; } = executionInstanceId;
    public string PointKey { get; } = pointKey;
    public string WriterFlowId { get; } = writerFlowId;
}

namespace Server.Services;

public sealed class FlowDebugSessionNotFoundException(string sessionId)
    : Exception($"Debug session \"{sessionId}\" was not found.");

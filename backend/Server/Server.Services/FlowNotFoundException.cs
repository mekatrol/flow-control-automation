namespace Server.Services;

public sealed class FlowNotFoundException(string id)
    : Exception($"Flow {id} was not found.");
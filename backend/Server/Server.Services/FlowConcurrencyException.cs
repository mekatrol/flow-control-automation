namespace Server.Services;

public sealed class FlowConcurrencyException(string id, Exception innerException)
    : Exception($"Flow {id} was changed by another request.", innerException);
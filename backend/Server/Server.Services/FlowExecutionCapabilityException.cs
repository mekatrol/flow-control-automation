namespace Server.Services;

public sealed class FlowExecutionCapabilityException(string capability)
    : Exception($"The execution context does not support '{capability}'.");
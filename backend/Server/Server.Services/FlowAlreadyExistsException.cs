namespace Server.Services;

public sealed class FlowAlreadyExistsException(
    string id,
    string name,
    Exception? innerException = null)
    : Exception($"A flow with the ID '{id}' and name '{name}' already exists.", innerException);
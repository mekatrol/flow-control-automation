namespace Server.Services.Contracts;

public sealed record DebugNodeSnapshot(
    string NodeId,
    string State,
    string Quality,
    DebugTypedValue? TypedValue);

using Server.Common.Contracts;

namespace Server.Services.Contracts;

public sealed record DebugNodeSnapshot(
    string NodeId,
    string State,
    DataQuality Quality,
    DebugTypedValue? TypedValue);
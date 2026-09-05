using Server.Common.Models;

namespace Server.Services.Contracts;

public sealed record DebugNodeSnapshot(
    string NodeId,
    string State,
    DataQuality Quality,
    DebugTypedValue? TypedValue);
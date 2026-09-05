using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record DebugNodeSnapshot(
    string NodeId,
    string State,
    DataQualityType Quality,
    DebugTypedValue? TypedValue);
using Server.Common.Models;

namespace Server.Services.Contracts;

public sealed record DebugTypedValue(DataType DataType, bool? Value = null, double? Number = null, DataQuality Quality = DataQuality.Good);
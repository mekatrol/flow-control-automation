using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record DebugTypedValue(DataType DataType, bool? Value = null, double? Number = null, DataQualityType Quality = DataQualityType.Good);
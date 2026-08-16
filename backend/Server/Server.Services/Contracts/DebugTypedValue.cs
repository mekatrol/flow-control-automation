using Server.Services.Extensions;

namespace Server.Services.Contracts;

public sealed record DebugTypedValue(DataType DataType, bool? Value = null, double? Number = null, string Quality = DataQualityExtensions.Good);
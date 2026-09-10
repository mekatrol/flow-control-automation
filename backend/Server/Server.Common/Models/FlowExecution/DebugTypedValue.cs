namespace Server.Common.Models.FlowExecution;

public sealed record DebugTypedValue(DataType DataType, bool? Value = null, double? Number = null, DataQualityType Quality = DataQualityType.Good);
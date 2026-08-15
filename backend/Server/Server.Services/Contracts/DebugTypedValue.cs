namespace Server.Services.Contracts;

public sealed record DebugTypedValue(string Type, bool? Value = null, double? Number = null, string Quality = "good");
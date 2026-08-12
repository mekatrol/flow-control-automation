namespace Server.Services.Contracts;

public sealed record FlowVmInput(string PointId, bool Value, bool IsGood = true);

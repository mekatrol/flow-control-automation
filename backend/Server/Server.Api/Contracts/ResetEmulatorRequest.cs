namespace Server.Api.Contracts;

public sealed record ResetEmulatorRequest(bool PowerCycle = false);

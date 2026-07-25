namespace Server.Services.Contracts;

public sealed record PointSafetyPolicy(
    string Startup,
    string Shutdown,
    string CommunicationLoss,
    string Disable);

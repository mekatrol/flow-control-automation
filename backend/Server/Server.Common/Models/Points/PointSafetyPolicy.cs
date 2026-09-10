namespace Server.Common.Models.Points;

public sealed record PointSafetyPolicy(
    string Startup,
    string Shutdown,
    string CommunicationLoss,
    string Disable);
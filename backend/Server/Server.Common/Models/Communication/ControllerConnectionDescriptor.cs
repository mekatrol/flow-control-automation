namespace Server.Common.Models.Communication;

public sealed record ControllerConnectionDescriptor(
    string Id,
    string Transport,
    string Address);
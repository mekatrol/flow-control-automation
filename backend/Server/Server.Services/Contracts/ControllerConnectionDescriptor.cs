namespace Server.Services.Contracts;

public sealed record ControllerConnectionDescriptor(
    string Id,
    string Transport,
    string Address);

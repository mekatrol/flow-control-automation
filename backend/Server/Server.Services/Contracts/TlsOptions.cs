namespace Server.Services.Contracts;

public sealed record TlsOptions
{
    public bool VerifyServerCertificate { get; init; }
}
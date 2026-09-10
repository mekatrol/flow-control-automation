namespace Server.Common.Models.Communication;

public sealed record TlsOptions
{
    public bool VerifyServerCertificate { get; init; }
}
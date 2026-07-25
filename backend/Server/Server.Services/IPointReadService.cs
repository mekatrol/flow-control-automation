using Server.Services.Contracts;

namespace Server.Services;

public interface IPointReadService
{
    Task<PointRuntimeEnvelope> ReadAsync(
        string pointId,
        CancellationToken cancellationToken);
}
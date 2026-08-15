using Server.Services.Contracts;

namespace Server.Services;

/// <summary>Reads current runtime values through the authoritative point abstraction.</summary>
public interface IPointReadService
{
    /// <summary>Reads one configured point and returns its value together with quality and freshness metadata.</summary>
    /// <param name="pointId">The non-empty canonical identifier of an enabled readable point.</param>
    /// <param name="cancellationToken">Cancels external I/O or waiting without changing point state.</param>
    /// <returns>A typed runtime envelope for the requested point, including an unavailable quality when the source cannot provide a value.</returns>
    Task<PointRuntimeEnvelope> ReadAsync(
        string pointId,
        CancellationToken cancellationToken);
}
using Server.Services.Contracts;

namespace Server.Services;

public interface IControllerDebugTransport
{
    Task<ControllerDebugLoadResult> LoadAsync(
        ReadOnlyMemory<byte> artifact,
        bool replaceExisting,
        CancellationToken cancellationToken);

    Task<ControllerDebugWireStatus> PrepareAsync(
        ulong sessionId,
        CancellationToken cancellationToken);

    Task<ControllerDebugWireStatus> GetStatusAsync(
        ulong sessionId,
        CancellationToken cancellationToken);

    Task<ControllerDebugSnapshotEnvelope> StepAsync(
        ulong sessionId,
        CancellationToken cancellationToken);

    Task RenewLeaseAsync(ulong sessionId, CancellationToken cancellationToken);

    Task StopAsync(ulong sessionId, CancellationToken cancellationToken);
}

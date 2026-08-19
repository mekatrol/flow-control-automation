using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Server.Services.Implementation;

public sealed class FcpControllerDebugTransport(IFcpClient client) : IControllerDebugTransport
{
    private const int MaximumAttempts = 3;
    private const int DigestBytes = 32;
    private static int _nextRequestId;

    public async Task<ControllerDebugLoadResult> LoadAsync(
        ReadOnlyMemory<byte> artifact,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        if (artifact.IsEmpty || artifact.Length > 16384)
        {
            throw new ArgumentOutOfRangeException(nameof(artifact));
        }

        var begin = new byte[41];
        BinaryPrimitives.WriteUInt32LittleEndian(
            begin,
            unchecked((uint)Interlocked.Increment(ref _nextRequestId)));
        begin[4] = replaceExisting ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(begin.AsSpan(5), checked((uint)artifact.Length));
        SHA256.HashData(artifact.Span).CopyTo(begin, 9);
        var beginResponse = await Exchange(0x50, begin, cancellationToken);
        RequireLength(beginResponse, 14, "debug load response");
        var load = new ControllerDebugLoadResult(
            BinaryPrimitives.ReadUInt64LittleEndian(beginResponse.Span),
            BinaryPrimitives.ReadUInt16LittleEndian(beginResponse.Span[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(beginResponse.Span[10..]));
        if (load.SessionId == 0 || load.ChunkLimit is 0 or > 180 || load.LeaseMilliseconds == 0)
        {
            throw Protocol("debug load response contains invalid bounds");
        }

        for (var offset = 0; offset < artifact.Length; offset += load.ChunkLimit)
        {
            var size = Math.Min(load.ChunkLimit, artifact.Length - offset);
            var chunk = new byte[12 + size];
            BinaryPrimitives.WriteUInt64LittleEndian(chunk, load.SessionId);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(8), checked((uint)offset));
            artifact.Span.Slice(offset, size).CopyTo(chunk.AsSpan(12));
            var chunkResponse = await Exchange(0x51, chunk, cancellationToken);
            RequireLength(chunkResponse, 6, "debug chunk response");
            if (BinaryPrimitives.ReadUInt32LittleEndian(chunkResponse.Span) != offset
                || BinaryPrimitives.ReadUInt16LittleEndian(chunkResponse.Span[4..]) != size)
            {
                throw Protocol("debug chunk acknowledgement does not match request");
            }
        }

        return load;
    }

    public async Task<ControllerDebugWireStatus> PrepareAsync(
        ulong sessionId,
        CancellationToken cancellationToken) =>
        ParseStatus(await Exchange(0x52, SessionBody(sessionId), cancellationToken));

    public async Task<ControllerDebugWireStatus> GetStatusAsync(
        ulong sessionId,
        CancellationToken cancellationToken) =>
        ParseStatus(await Exchange(0x53, SessionBody(sessionId), cancellationToken));

    public async Task<ControllerDebugSnapshotEnvelope> StepAsync(
        ulong sessionId,
        CancellationToken cancellationToken)
    {
        var step = await Exchange(0x54, SessionBody(sessionId), cancellationToken);
        RequireLength(step, 44, "debug step response");
        var tick = BinaryPrimitives.ReadUInt64LittleEndian(step.Span);
        var expectedLength = BinaryPrimitives.ReadUInt32LittleEndian(step.Span[8..]);
        var expectedDigest = step.Slice(12, DigestBytes).ToArray();

        return await ReadSnapshotAsync(sessionId, tick, expectedLength, expectedDigest, cancellationToken);
    }

    public Task<ControllerDebugWireStatus> RunAsync(ulong sessionId, uint intervalMilliseconds, CancellationToken cancellationToken)
    {
        if (intervalMilliseconds is < 10 or > 60000)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
        }

        var body = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(body, sessionId);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(8), intervalMilliseconds);
        return RunCoreAsync(body, cancellationToken);
    }

    private async Task<ControllerDebugWireStatus> RunCoreAsync(byte[] body, CancellationToken cancellationToken) =>
        ParseStatus(await Exchange(0x59, body, cancellationToken));

    public async Task<ControllerDebugWireStatus> PauseAsync(ulong sessionId, CancellationToken cancellationToken) =>
        ParseStatus(await Exchange(0x5a, SessionBody(sessionId), cancellationToken));

    public async Task<ControllerDebugLiveOutputResult> EnableLiveOutputAsync(
        ulong sessionId,
        IReadOnlyList<string> confirmedPointIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmedPointIds);
        if (confirmedPointIds.Count is 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmedPointIds));
        }
        var encoded = confirmedPointIds.Select(pointId =>
        {
            var bytes = Encoding.UTF8.GetBytes(pointId);
            if (bytes.Length is 0 or > 63)
            {
                throw new ArgumentException("Confirmed output point IDs must contain 1-63 UTF-8 bytes.", nameof(confirmedPointIds));
            }
            return bytes;
        }).ToArray();
        var bodyLength = 9 + encoded.Sum(bytes => 1 + bytes.Length);
        if (bodyLength > 241)
        {
            throw new ArgumentException("Confirmed output point list exceeds the controller frame limit.", nameof(confirmedPointIds));
        }
        var body = new byte[bodyLength];
        BinaryPrimitives.WriteUInt64LittleEndian(body, sessionId);
        body[8] = checked((byte)encoded.Length);
        var offset = 9;
        foreach (var pointId in encoded)
        {
            body[offset++] = checked((byte)pointId.Length);
            pointId.CopyTo(body, offset);
            offset += pointId.Length;
        }
        var response = await Exchange(0x5b, body, cancellationToken);
        RequireLength(response, 5, "enable live output response");
        var priority = response.Span[0];
        var holdMilliseconds = BinaryPrimitives.ReadUInt32LittleEndian(response.Span[1..]);
        if (priority is < 1 or > 16 || holdMilliseconds is 0 or > 1000)
        {
            throw Protocol("controller returned unsafe live-output policy");
        }
        return new(priority, holdMilliseconds);
    }

    public async Task<ControllerDebugSnapshotEnvelope> ReadSnapshotAsync(
        ulong sessionId, ulong tickNumber, CancellationToken cancellationToken) =>
        await ReadSnapshotAsync(sessionId, tickNumber, null, null, cancellationToken);

    private async Task<ControllerDebugSnapshotEnvelope> ReadSnapshotAsync(
        ulong sessionId, ulong tick, uint? expectedLength, byte[]? expectedDigest, CancellationToken cancellationToken)
    {
        var headerBody = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(headerBody, sessionId);
        BinaryPrimitives.WriteUInt64LittleEndian(headerBody.AsSpan(8), tick);
        var header = await Exchange(0x55, headerBody, cancellationToken);
        RequireLength(header, 56, "snapshot header response");
        var headerSession = BinaryPrimitives.ReadUInt64LittleEndian(header.Span);
        var headerTick = BinaryPrimitives.ReadUInt64LittleEndian(header.Span[8..]);
        var totalLength = BinaryPrimitives.ReadUInt32LittleEndian(header.Span[16..]);
        var chunkCount = BinaryPrimitives.ReadUInt16LittleEndian(header.Span[20..]);
        var chunkLimit = BinaryPrimitives.ReadUInt16LittleEndian(header.Span[22..]);
        expectedLength ??= totalLength;
        expectedDigest ??= header.Slice(24, DigestBytes).ToArray();
        if (headerSession != sessionId || headerTick != tick || totalLength != expectedLength
            || totalLength is 0 or > 16384 || chunkCount == 0 || chunkLimit == 0
            || !header.Span.Slice(24, DigestBytes).SequenceEqual(expectedDigest))
        {
            throw Protocol("snapshot header does not match step response");
        }

        var bytes = new byte[totalLength];
        var covered = 0;
        for (ushort index = 0; index < chunkCount; index++)
        {
            var request = new byte[18];
            BinaryPrimitives.WriteUInt64LittleEndian(request, sessionId);
            BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(8), tick);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(16), index);
            var chunk = await Exchange(0x56, request, cancellationToken);
            if (chunk.Length < 24
                || BinaryPrimitives.ReadUInt64LittleEndian(chunk.Span) != sessionId
                || BinaryPrimitives.ReadUInt64LittleEndian(chunk.Span[8..]) != tick
                || BinaryPrimitives.ReadUInt16LittleEndian(chunk.Span[16..]) != index
                || BinaryPrimitives.ReadUInt16LittleEndian(chunk.Span[18..]) != chunkCount
                || BinaryPrimitives.ReadUInt32LittleEndian(chunk.Span[20..]) != covered)
            {
                throw Protocol("snapshot chunk metadata is inconsistent");
            }

            var data = chunk[24..];
            if (data.IsEmpty || data.Length > chunkLimit || covered + data.Length > bytes.Length
                || (index + 1 < chunkCount && data.Length != chunkLimit))
            {
                throw Protocol("snapshot chunk length is invalid");
            }
            data.Span.CopyTo(bytes.AsSpan(covered));
            covered += data.Length;
        }

        if (covered != bytes.Length || !SHA256.HashData(bytes).AsSpan().SequenceEqual(expectedDigest))
        {
            throw Protocol("snapshot is incomplete or failed digest validation");
        }
        return new(sessionId, tick, bytes, expectedDigest);
    }

    public async Task RenewLeaseAsync(ulong sessionId, CancellationToken cancellationToken)
    {
        var response = await Exchange(0x57, SessionBody(sessionId), cancellationToken);
        RequireLength(response, 4, "renew lease response");
    }

    public async Task StopAsync(ulong sessionId, CancellationToken cancellationToken)
    {
        var response = await Exchange(0x58, SessionBody(sessionId), cancellationToken);
        RequireLength(response, 8, "debug stop response");
        if (BinaryPrimitives.ReadUInt64LittleEndian(response.Span) != sessionId)
        {
            throw Protocol("stopped session ID does not match request");
        }
    }

    private async Task<ReadOnlyMemory<byte>> Exchange(
        byte operation,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await client.ExchangeAuthenticatedAsync(operation, payload, cancellationToken);
            }
            catch (Exception exception) when (
                attempt < MaximumAttempts
                && exception is IOException or TimeoutException
                && !cancellationToken.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ControllerGatewayException("transport", "Controller exchange failed.", exception);
            }
        }
    }

    private static ControllerDebugWireStatus ParseStatus(ReadOnlyMemory<byte> response)
    {
        if (response.Length < 36)
        {
            throw Protocol("debug status response is truncated");
        }
        var span = response.Span;
        var pathLength = span[35];
        if (pathLength > 63 || response.Length != 36 + pathLength)
        {
            throw Protocol("debug status reason path has invalid length");
        }
        return new(
            BinaryPrimitives.ReadUInt64LittleEndian(span),
            span[8],
            BinaryPrimitives.ReadUInt32LittleEndian(span[9..]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[13..]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[17..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[21..]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[29..]),
            BinaryPrimitives.ReadUInt16LittleEndian(span[33..]),
            System.Text.Encoding.UTF8.GetString(span.Slice(36, pathLength)));
    }

    private static byte[] SessionBody(ulong sessionId)
    {
        if (sessionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionId));
        }
        var body = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(body, sessionId);
        return body;
    }

    private static void RequireLength(ReadOnlyMemory<byte> response, int length, string name)
    {
        if (response.Length != length)
        {
            throw Protocol($"{name} has invalid length");
        }
    }

    private static ControllerGatewayException Protocol(string message) => new("protocol", message);
}
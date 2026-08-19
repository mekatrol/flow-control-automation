using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Server.Services.Implementation;

public sealed class FcpAuthenticatedClient(
    IFcpFrameTransport transport,
    FcpClientOptions options) : IFcpClient
{
    private const byte Version = 1;
    private const byte ResponseFlag = 1;
    private const byte ErrorFlag = 2;
    private const byte AuthenticatedFlag = 4;
    private const int HeaderBytes = 13;
    private const int TagBytes = 32;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private uint _sessionId;
    private ulong _sequence;
    private ushort _transaction;

    public async Task<ReadOnlyMemory<byte>> ExchangeAuthenticatedAsync(
        byte operation,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (options.AuthenticationKey.Length != TagBytes || payload.Length > 197)
        {
            throw new ArgumentException("FCP authentication key or payload is outside protocol bounds.");
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_sessionId == 0)
            {
                await Authenticate(cancellationToken);
            }
            var sequence = ++_sequence;
            var body = new byte[12 + payload.Length + TagBytes];
            BinaryPrimitives.WriteUInt32LittleEndian(body, _sessionId);
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(4), sequence);
            payload.Span.CopyTo(body.AsSpan(12));
            Sign("FCP1REQT"u8, sequence, operation, payload.Span, body.AsSpan(12 + payload.Length));
            var response = await Exchange(operation, body, AuthenticatedFlag, cancellationToken);
            if ((response.Flags & AuthenticatedFlag) == 0 || response.Payload.Length < 12 + TagBytes)
            {
                throw Protocol("missing authenticated response envelope");
            }
            var responseSpan = response.Payload.Span;
            var responseSession = BinaryPrimitives.ReadUInt32LittleEndian(responseSpan);
            var responseSequence = BinaryPrimitives.ReadUInt64LittleEndian(responseSpan[4..]);
            var responseBody = response.Payload[12..^TagBytes];
            Span<byte> expected = stackalloc byte[TagBytes];
            Sign("FCP1RESP"u8, responseSequence, operation, responseBody.Span, expected);
            if (responseSession != _sessionId || responseSequence != sequence
                || !CryptographicOperations.FixedTimeEquals(expected, responseSpan[^TagBytes..]))
            {
                throw Protocol("authenticated response verification failed");
            }
            if ((response.Flags & ErrorFlag) != 0)
            {
                var code = responseBody.Length >= 2
                    ? BinaryPrimitives.ReadUInt16LittleEndian(responseBody.Span)
                    : (ushort)0;
                throw new ControllerGatewayException(ErrorCategory(code), $"Controller returned FCP error {code}.");
            }
            return responseBody;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task Authenticate(CancellationToken cancellationToken)
    {
        var clientNonce = RandomNumberGenerator.GetBytes(16);
        var challenge = await Exchange(0x30, clientNonce, 0, cancellationToken);
        if (challenge.Payload.Length != 20 || (challenge.Flags & ErrorFlag) != 0)
        {
            throw new ControllerGatewayException("authentication", "Controller authentication challenge failed.");
        }
        _sessionId = BinaryPrimitives.ReadUInt32LittleEndian(challenge.Payload.Span);
        var transcript = new byte[8 + 2 + 4 + 16 + 16];
        "FCP1PROF"u8.CopyTo(transcript);
        BinaryPrimitives.WriteUInt16LittleEndian(transcript.AsSpan(8), options.HostAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(transcript.AsSpan(10), _sessionId);
        clientNonce.CopyTo(transcript, 14);
        challenge.Payload.Span[4..].CopyTo(transcript.AsSpan(30));
        var proofBody = new byte[4 + TagBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(proofBody, _sessionId);
        HMACSHA256.HashData(options.AuthenticationKey, transcript).CopyTo(proofBody, 4);
        var proof = await Exchange(0x31, proofBody, 0, cancellationToken);
        if ((proof.Flags & ErrorFlag) != 0 || proof.Payload.Length != 4
            || BinaryPrimitives.ReadUInt32LittleEndian(proof.Payload.Span) != _sessionId)
        {
            _sessionId = 0;
            throw new ControllerGatewayException("authentication", "Controller authentication proof failed.");
        }
    }

    private async Task<FcpResponse> Exchange(
        byte operation,
        ReadOnlyMemory<byte> payload,
        byte flags,
        CancellationToken cancellationToken)
    {
        var transaction = ++_transaction;
        var frame = EncodeFrame(operation, payload.Span, flags, transaction);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);
        var responseBytes = await transport.TransceiveAsync(frame, timeout.Token);
        return DecodeFrame(responseBytes.Span, operation, transaction);
    }

    private byte[] EncodeFrame(byte operation, ReadOnlySpan<byte> payload, byte flags, ushort transaction)
    {
        var frame = new byte[HeaderBytes + payload.Length + 2];
        frame[0] = 0x46;
        frame[1] = 0x43;
        frame[2] = Version;
        frame[3] = flags;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), options.ControllerAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), options.HostAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), transaction);
        frame[10] = operation;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(11), checked((ushort)payload.Length));
        payload.CopyTo(frame.AsSpan(HeaderBytes));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(^2), Crc(frame.AsSpan(..^2)));
        return frame;
    }

    private FcpResponse DecodeFrame(ReadOnlySpan<byte> frame, byte operation, ushort transaction)
    {
        if (frame.Length < 15 || frame.Length > 256 || frame[0] != 0x46 || frame[1] != 0x43 || frame[2] != Version
            || (frame[3] & ResponseFlag) == 0
            || BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]) != options.HostAddress
            || BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]) != options.ControllerAddress
            || BinaryPrimitives.ReadUInt16LittleEndian(frame[8..]) != transaction || frame[10] != operation
            || BinaryPrimitives.ReadUInt16LittleEndian(frame[11..]) != frame.Length - 15
            || BinaryPrimitives.ReadUInt16LittleEndian(frame[^2..]) != Crc(frame[..^2]))
        {
            throw Protocol("invalid or mismatched FCP response frame");
        }
        return new(frame[3], frame[HeaderBytes..^2].ToArray());
    }

    private void Sign(
        ReadOnlySpan<byte> domain,
        ulong sequence,
        byte operation,
        ReadOnlySpan<byte> body,
        Span<byte> destination)
    {
        var transcript = new byte[domain.Length + 2 + 4 + 8 + 1 + body.Length];
        domain.CopyTo(transcript);
        var offset = domain.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(transcript.AsSpan(offset), options.HostAddress);
        offset += 2;
        BinaryPrimitives.WriteUInt32LittleEndian(transcript.AsSpan(offset), _sessionId);
        offset += 4;
        BinaryPrimitives.WriteUInt64LittleEndian(transcript.AsSpan(offset), sequence);
        offset += 8;
        transcript[offset++] = operation;
        body.CopyTo(transcript.AsSpan(offset));
        HMACSHA256.HashData(options.AuthenticationKey, transcript, destination);
    }

    private static ushort Crc(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xffff;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xa001) : (ushort)(crc >> 1);
            }
        }
        return crc;
    }

    private static string ErrorCategory(ushort code) => code switch
    {
        9 or 10 or 11 => "authentication",
        18 => "validation",
        6 => "stale_session",
        12 => "busy",
        _ => "protocol"
    };

    private static ControllerGatewayException Protocol(string message) => new("protocol", message);

    private sealed record FcpResponse(byte Flags, ReadOnlyMemory<byte> Payload);
}
using System.Buffers.Binary;

namespace Server.Services.Implementation;

public sealed class SerialRs485FrameTransport(IControllerSerialConnectionFactory connections) : IFcpFrameTransport
{
    private const int HeaderBytes = 13;
    private const int CrcBytes = 2;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Stream? _stream;

    public async Task<ReadOnlyMemory<byte>> TransceiveAsync(
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken)
    {
        if (request.IsEmpty || request.Length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _stream ??= await connections.ConnectAsync(cancellationToken);
            await _stream.WriteAsync(request, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            var header = new byte[HeaderBytes];
            await ReadExactly(header, cancellationToken);
            var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(11));
            if (HeaderBytes + payloadLength + CrcBytes > 256)
            {
                throw new ControllerGatewayException("protocol", "FCP response exceeds the frame bound.");
            }
            var frame = new byte[HeaderBytes + payloadLength + CrcBytes];
            header.CopyTo(frame, 0);
            await ReadExactly(frame.AsMemory(HeaderBytes), cancellationToken);
            return frame;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (_stream is not null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }
            throw new ControllerGatewayException("transport", "Serial FCP exchange failed.", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReadExactly(Memory<byte> destination, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await _stream!.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Controller closed the serial connection.");
            }
            offset += read;
        }
    }
}

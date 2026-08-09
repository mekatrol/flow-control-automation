using Server.Services;
using Server.Services.Implementation;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Tests.Unit.Flows;

public sealed class FcpControllerDebugTransportTests
{
    [Test]
    public async Task LoadsArtifactInNegotiatedBoundedChunks()
    {
        var client = new RecordingFcpClient();
        var artifact = Enumerable.Range(0, 401).Select(index => (byte)index).ToArray();

        var result = await new FcpControllerDebugTransport(client)
            .LoadAsync(artifact, replaceExisting: false, default);

        Assert.Multiple(() =>
        {
            Assert.That(result.SessionId, Is.EqualTo(42));
            Assert.That(client.Operations, Is.EqualTo(new byte[] { 0x50, 0x51, 0x51, 0x51 }));
            Assert.That(client.Uploaded.ToArray(), Is.EqualTo(artifact));
        });
    }

    [Test]
    public async Task ReassemblesAndValidatesSnapshotChunks()
    {
        var snapshot = SnapshotBytes();
        var client = new RecordingFcpClient(snapshot);

        var envelope = await new FcpControllerDebugTransport(client).StepAsync(42, default);
        var decoded = DebugSnapshotDecoder.Decode(envelope);

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Bytes.ToArray(), Is.EqualTo(snapshot));
            Assert.That(decoded.DebugSessionId, Is.EqualTo("42"));
            Assert.That(decoded.FlowId, Is.EqualTo("flow-a"));
            Assert.That(decoded.TickNumber, Is.EqualTo(1));
            Assert.That(decoded.Nodes, Is.Empty);
        });
    }

    private static byte[] SnapshotBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)1);
        writer.Write((ulong)42);
        writer.Write((byte)6);
        writer.Write(Encoding.UTF8.GetBytes("flow-a"));
        writer.Write((uint)3);
        writer.Write((byte)4);
        writer.Write((byte)1);
        writer.Write((ulong)1);
        writer.Write((ulong)1000);
        writer.Write((ulong)1001);
        writer.Write((uint)10);
        writer.Write((byte)7);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((uint)0);
        writer.Write((uint)0);
        writer.Write((ushort)0);
        writer.Write((byte)0);
        return stream.ToArray();
    }

    private sealed class RecordingFcpClient(byte[]? snapshot = null) : IFcpClient
    {
        private readonly List<byte> _uploaded = [];

        public IReadOnlyList<byte> Operations { get; } = new List<byte>();
        public IReadOnlyList<byte> Uploaded => _uploaded;

        public Task<ReadOnlyMemory<byte>> ExchangeAuthenticatedAsync(
            byte operation,
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken)
        {
            ((List<byte>)Operations).Add(operation);
            return Task.FromResult(operation switch
            {
                0x50 => Begin(),
                0x51 => Chunk(payload),
                0x54 => Step(),
                0x55 => Header(),
                0x56 => SnapshotChunk(payload),
                _ => throw new InvalidOperationException()
            });
        }

        private static ReadOnlyMemory<byte> Begin()
        {
            var response = new byte[14];
            BinaryPrimitives.WriteUInt64LittleEndian(response, 42);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(8), 180);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(10), 30000);
            return response;
        }

        private ReadOnlyMemory<byte> Chunk(ReadOnlyMemory<byte> request)
        {
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(request.Span[8..]);
            _uploaded.AddRange(request[12..].ToArray());
            var response = new byte[6];
            BinaryPrimitives.WriteUInt32LittleEndian(response, offset);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(4), checked((ushort)(request.Length - 12)));
            return response;
        }

        private ReadOnlyMemory<byte> Step()
        {
            var response = new byte[44];
            BinaryPrimitives.WriteUInt64LittleEndian(response, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8), checked((uint)snapshot!.Length));
            SHA256.HashData(snapshot).CopyTo(response, 12);
            return response;
        }

        private ReadOnlyMemory<byte> Header()
        {
            var response = new byte[56];
            BinaryPrimitives.WriteUInt64LittleEndian(response, 42);
            BinaryPrimitives.WriteUInt64LittleEndian(response.AsSpan(8), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(16), checked((uint)snapshot!.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(20), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(22), 173);
            SHA256.HashData(snapshot).CopyTo(response, 24);
            return response;
        }

        private ReadOnlyMemory<byte> SnapshotChunk(ReadOnlyMemory<byte> request)
        {
            var response = new byte[24 + snapshot!.Length];
            request.Span[..16].CopyTo(response);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(16), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(18), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(20), 0);
            snapshot.CopyTo(response, 24);
            return response;
        }
    }
}

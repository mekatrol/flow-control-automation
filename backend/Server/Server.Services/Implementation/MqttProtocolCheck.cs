using Server.Services.Contracts;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class MqttProtocolCheck(IConnectivityClock clock) : IMqttProtocolCheck
{
    public async Task<string?> CheckAsync(
        Stream stream,
        PointSource source,
        string credential,
        CancellationToken cancellationToken)
    {
        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(
            source.Timeouts.RequestMilliseconds
            ?? source.Timeouts.ConnectMilliseconds));
        var operationToken = timeoutSource.Token;
        var clientId = $"{source.Connection.ClientIdPrefix}-test-"
            + clock.UtcNow.ToUnixTimeMilliseconds().ToString("x");
        var payload = new List<byte>(
            [0, 4, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 4, 2, 0, 10]);
        payload.AddRange(MqttString(clientId));
        if (credential.Length > 0)
        {
            MqttLogin? login;
            try
            {
                login = JsonSerializer.Deserialize<MqttLogin>(
                    credential,
                    FlowControlJson.Options);
            }
            catch (JsonException)
            {
                return "MQTT credential must be JSON with username and password";
            }

            if (string.IsNullOrEmpty(login?.Username))
            {
                return "MQTT credential must be JSON with username and password";
            }

            payload[7] |= 0xc0;
            payload.AddRange(MqttString(login.Username));
            payload.AddRange(MqttString(login.Password ?? string.Empty));
        }

        try
        {
            await stream.WriteAsync(Packet(0x10, payload), operationToken);
            var reply = new byte[4];
            await stream.ReadExactlyAsync(reply, operationToken);
            if (reply[0] != 0x20 || reply[3] != 0)
            {
                return "MQTT CONNACK rejected";
            }

            if (!string.IsNullOrEmpty(source.Connection.TestTopic))
            {
                var subscribe = new List<byte>([0, 1]);
                subscribe.AddRange(MqttString(source.Connection.TestTopic));
                subscribe.Add((byte)source.Connection.Qos!.Value);
                await stream.WriteAsync(Packet(0x82, subscribe), operationToken);
                var header = new byte[2];
                await stream.ReadExactlyAsync(header, operationToken);
                if (header[0] != 0x90 || header[1] < 3)
                {
                    return "invalid MQTT SUBACK";
                }

                var suback = new byte[header[1]];
                await stream.ReadExactlyAsync(suback, operationToken);
                if (suback[^1] == 0x80)
                {
                    return "MQTT topic subscription rejected";
                }
            }

            await stream.WriteAsync(new byte[] { 0xe0, 0 }, operationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return "connection test cancelled";
        }
        catch (OperationCanceledException)
        {
            return "MQTT protocol check failed";
        }
        catch
        {
            return "MQTT protocol check failed";
        }
    }

    private static byte[] Packet(byte packetType, IReadOnlyCollection<byte> payload)
    {
        var packet = new List<byte> { packetType };
        var remaining = payload.Count;
        do
        {
            var encoded = (byte)(remaining % 128);
            remaining /= 128;
            if (remaining > 0)
            {
                encoded |= 0x80;
            }

            packet.Add(encoded);
        }
        while (remaining > 0);

        packet.AddRange(payload);
        return [.. packet];
    }

    private static byte[] MqttString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var result = new byte[bytes.Length + 2];
        BinaryPrimitives.WriteUInt16BigEndian(result, checked((ushort)bytes.Length));
        bytes.CopyTo(result, 2);
        return result;
    }

    private sealed record MqttLogin(string Username, string Password);
}
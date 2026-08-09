using Server.Services.Contracts;
using System.Buffers.Binary;
using System.Text;

namespace Server.Services.Implementation;

public static class DebugSnapshotDecoder
{
    private const ulong MaximumSafeJsonInteger = 9_007_199_254_740_991;

    public static DebugRuntimeSnapshot Decode(ControllerDebugSnapshotEnvelope envelope)
    {
        var reader = new SnapshotReader(envelope.Bytes.Span);
        if (reader.ReadUInt16() != 1)
        {
            throw Protocol("unsupported snapshot schema");
        }
        var sessionId = reader.ReadUInt64();
        var flowId = reader.ReadString();
        var revision = reader.ReadUInt32();
        var state = Name(StateNames, reader.ReadByte(), "lifecycle state");
        var mode = reader.ReadByte() == 1 ? "manual" : throw Protocol("unknown execution mode");
        var tick = reader.ReadUInt64();
        var sampledAt = reader.ReadUInt64();
        var completedAt = reader.ReadUInt64();
        var duration = reader.ReadUInt32();
        var validity = reader.ReadByte();
        var nodeCount = reader.ReadUInt16();
        var outputCount = reader.ReadUInt16();
        var overrunCount = reader.ReadUInt32();
        var failureCount = reader.ReadUInt32();
        var reasonCode = reader.ReadUInt16();
        var reasonPath = reader.ReadString(allowEmpty: true);
        if (sessionId != envelope.SessionId || tick != envelope.TickNumber || completedAt < sampledAt
            || sessionId > MaximumSafeJsonInteger || tick > MaximumSafeJsonInteger
            || sampledAt > MaximumSafeJsonInteger || completedAt > MaximumSafeJsonInteger
            || validity > 7 || nodeCount > 128 || outputCount > 64)
        {
            throw Protocol("snapshot metadata is inconsistent or outside JSON bounds");
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new List<DebugNodeSnapshot>(nodeCount);
        for (var index = 0; index < nodeCount; index++)
        {
            var nodeId = reader.ReadString();
            var nodeState = Name(NodeStateNames, reader.ReadByte(), "node state");
            var quality = Name(QualityNames, reader.ReadByte(), "quality");
            if (reader.ReadByte() != 2)
            {
                throw Protocol("unknown snapshot value type");
            }
            var isPresent = reader.ReadBoolean();
            var typedValue = isPresent ? new DebugTypedValue("digital", reader.ReadBoolean()) : null;
            if (!nodeIds.Add(nodeId) || (nodeState == "evaluated" && quality == "good" && typedValue is null))
            {
                throw Protocol("node snapshot is duplicated or missing a required value");
            }
            nodes.Add(new(nodeId, nodeState, quality, typedValue));
        }

        var outputIds = new HashSet<string>(StringComparer.Ordinal);
        var outputs = new List<DebugProposedOutput>(outputCount);
        for (var index = 0; index < outputCount; index++)
        {
            var pointId = reader.ReadString();
            var outputState = Name(NodeStateNames, reader.ReadByte(), "output state");
            var quality = Name(QualityNames, reader.ReadByte(), "quality");
            var value = reader.ReadBoolean();
            if (!outputIds.Add(pointId))
            {
                throw Protocol("proposed output is duplicated");
            }
            outputs.Add(new(pointId, outputState, quality, value));
        }
        reader.RequireEnd();

        var inputValidity = new List<string>(3);
        if ((validity & 1) != 0)
        {
            inputValidity.Add("coherent");
        }
        if ((validity & 2) != 0)
        {
            inputValidity.Add("all_present");
        }
        if ((validity & 4) != 0)
        {
            inputValidity.Add("all_good");
        }

        return new DebugRuntimeSnapshot
        {
            DebugSessionId = sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FlowId = flowId,
            Revision = revision,
            LifecycleState = state,
            Mode = mode,
            TickNumber = tick,
            SampledAtMs = sampledAt,
            CompletedAtMs = completedAt,
            ExecutionDurationUs = duration,
            InputValidity = inputValidity,
            Nodes = nodes,
            ProposedOutputs = outputs,
            OverrunCount = overrunCount,
            EvaluationFailureCount = failureCount,
            LastReasonCode = reasonCode,
            LastReason = ReasonName(reasonCode),
            LastReasonPath = reasonPath
        };
    }

    private static readonly string[] StateNames =
        ["empty", "loading", "ready", "stepping", "paused", "fault", "stopped"];
    private static readonly string[] NodeStateNames = ["idle", "evaluated", "fault", "unavailable"];
    private static readonly string[] QualityNames = ["good", "uncertain", "bad", "unavailable"];
    private static readonly string[] ReasonNames =
    [
        "ok", "malformed", "unsupported_schema", "length_mismatch", "digest_mismatch", "limit_exceeded",
        "invalid_identifier", "non_canonical_order", "unknown_node_kind", "invalid_configuration",
        "invalid_port_shape", "missing_connection", "duplicate_driver", "incompatible_type", "missing_point",
        "point_direction_mismatch", "combinational_cycle", "unsupported_mode", "unsupported_capability",
        "snapshot_too_large", "input_quality_rejected", "evaluation_failed"
    ];

    private static string Name(IReadOnlyList<string> names, byte value, string field) =>
        value < names.Count ? names[value] : throw Protocol($"unknown {field}");

    private static string ReasonName(ushort reason) =>
        reason < ReasonNames.Length ? ReasonNames[reason] : $"unknown_{reason}";

    private static ControllerGatewayException Protocol(string message) => new("protocol", message);

    private ref struct SnapshotReader(ReadOnlySpan<byte> bytes)
    {
        private readonly ReadOnlySpan<byte> _bytes = bytes;
        private int _offset;

        public byte ReadByte()
        {
            Require(1);
            return _bytes[_offset++];
        }

        public bool ReadBoolean()
        {
            var value = ReadByte();
            return value switch
            {
                0 => false,
                1 => true,
                _ => throw Protocol("invalid Boolean value")
            };
        }

        public ushort ReadUInt16()
        {
            Require(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_bytes[_offset..]);
            _offset += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            Require(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_bytes[_offset..]);
            _offset += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            Require(8);
            var value = BinaryPrimitives.ReadUInt64LittleEndian(_bytes[_offset..]);
            _offset += 8;
            return value;
        }

        public string ReadString(bool allowEmpty = false)
        {
            var size = ReadByte();
            if ((!allowEmpty && size == 0) || size > 63)
            {
                throw Protocol("invalid string length");
            }
            Require(size);
            string value;
            try
            {
                value = new UTF8Encoding(false, true).GetString(_bytes.Slice(_offset, size));
            }
            catch (DecoderFallbackException exception)
            {
                throw new ControllerGatewayException("protocol", "snapshot contains invalid UTF-8", exception);
            }
            _offset += size;
            return value;
        }

        public void RequireEnd()
        {
            if (_offset != _bytes.Length)
            {
                throw Protocol("snapshot contains trailing bytes");
            }
        }

        private void Require(int size)
        {
            if (size < 0 || _offset > _bytes.Length - size)
            {
                throw Protocol("snapshot is truncated");
            }
        }
    }
}

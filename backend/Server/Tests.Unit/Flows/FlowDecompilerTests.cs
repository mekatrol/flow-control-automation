using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Tests.Unit.Flows;

public sealed class FlowDecompilerTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v2");

    [TestCase("valid-two-button-and")]
    [TestCase("valid-memory-feedback")]
    [TestCase("valid-expanded-boolean")]
    [TestCase("valid-numeric-language")]
    [TestCase("valid-analog-points")]
    public void RecompilesRecoveredDesignerSemanticsToTheIdenticalArtifact(string fixture)
    {
        var artifact = Artifact(fixture);
        var recovered = new FlowDecompiler().Decompile(artifact);
        var source = new ExecutableFlowSource
        {
            Id = recovered.Flow.Id,
            Revision = recovered.Provenance.FlowRevision,
            ControllerTemplateId = recovered.Provenance.ControllerTemplateId,
            ControllerTemplateRevision = recovered.Provenance.ControllerTemplateRevision,
            Nodes = recovered.Flow.Nodes.Select(node => new ExecutableFlowNode
            {
                Id = node.Id,
                Kind = node.Kind,
                Configuration = node.Configuration,
                Label = node.Label,
                X = node.X,
                Y = node.Y,
                ZOrder = node.ZOrder,
                GroupId = node.GroupId
            }).ToArray(),
            Connections = recovered.Flow.Connections.Select(connection => new ExecutableFlowConnection(
                new ExecutableFlowEndpoint(connection.Start.NodeId, connection.Start.ConnectorId),
                new ExecutableFlowEndpoint(connection.End.NodeId, connection.End.ConnectorId))).ToArray()
        };

        var recompiled = new FlowCompiler().Compile(CompilationRequest(source));

        Assert.That(recompiled.Artifact.ToArray(), Is.EqualTo(artifact));
    }

    [TestCase("valid-two-button-and", 4, 3)]
    [TestCase("valid-memory-feedback", 4, 4)]
    public void RecoversAValidDeterministicDesignerFlow(string fixture, int nodeCount, int connectionCount)
    {
        var artifact = Artifact(fixture);
        var decompiler = new FlowDecompiler();

        var first = decompiler.Decompile(artifact);
        var second = decompiler.Decompile(artifact);

        Assert.Multiple(() =>
        {
            Assert.That(
                System.Text.Json.JsonSerializer.Serialize(first, FlowControlJson.Options),
                Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(second, FlowControlJson.Options)));
            Assert.That(first.RecoveryLevel, Is.EqualTo("lossless"));
            Assert.That(first.Flow.Nodes, Has.Count.EqualTo(nodeCount));
            Assert.That(first.Flow.Connections, Has.Count.EqualTo(connectionCount));
            Assert.That(first.Warnings, Is.Empty);
            Assert.That(first.Provenance.ArtifactVersion, Is.EqualTo(2));
        });
    }

    [Test]
    public void PreservesExecutableNodeIdentityConfigurationAndFeedback()
    {
        var result = new FlowDecompiler().Decompile(Artifact("valid-memory-feedback"));
        var memory = result.Flow.Nodes.Single(node => node.Id == "memory-1");

        Assert.Multiple(() =>
        {
            Assert.That(memory.Kind, Is.EqualTo("memory"));
            Assert.That(memory.Configuration["value"].GetBoolean(), Is.False);
            Assert.That(result.Flow.Nodes.Single(node => node.Id == "output-01-node")
                .Configuration["pointId"].GetString(), Is.EqualTo("output-01"));
            Assert.That(result.Flow.Connections.Any(connection =>
                connection.Start.NodeId == "or-1"
                && connection.End == new FlowEndpoint("memory-1", "in")), Is.True);
        });
    }

    [Test]
    public void PreservesLosslessGroupAndCanvasMetadata()
    {
        var result = new FlowDecompiler().Decompile(Artifact("valid-analog-points"));

        Assert.Multiple(() =>
        {
            Assert.That(result.RecoveryLevel, Is.EqualTo("lossless"));
            Assert.That(result.Flow.Nodes.Single(node => node.Id == "shift").GroupId, Is.EqualTo("conditioning"));
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    public void RejectsCorruptArtifactsBeforeReadingInstructions()
    {
        var artifact = Artifact("valid-two-button-and");
        artifact[^1] ^= 1;

        var exception = Assert.Throws<FlowDecompilationException>(
            () => new FlowDecompiler().Decompile(artifact));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("invalid_digest"));
    }

    [Test]
    public void RejectsUnsupportedArtifactVersionsWithAStablePath()
    {
        var artifact = Artifact("valid-two-button-and");
        artifact[4] = 3;

        var exception = Assert.Throws<FlowDecompilationException>(
            () => new FlowDecompiler().Decompile(artifact));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("unsupported_version"));
            Assert.That(exception.Diagnostic.Path, Is.EqualTo("/version"));
        });
    }

    [Test]
    public void RecoversNormalizedDesignerFlowWhenAuthoringMetadataIsAbsent()
    {
        var artifact = StripAuthoringMetadata(Artifact("valid-two-button-and"));

        var result = new FlowDecompiler().Decompile(artifact);

        Assert.Multiple(() =>
        {
            Assert.That(result.RecoveryLevel, Is.EqualTo("normalized"));
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Flow.Nodes.All(node => node.Label.Length > 0), Is.True);
        });
    }

    private static byte[] StripAuthoringMetadata(byte[] artifact)
    {
        const int envelopeBytes = 128;
        const int entryBytes = 48;
        var sections = new List<byte[]>();
        var counts = new List<uint>();
        var versions = new List<ushort>();
        for (var index = 0; index < 8; index++)
        {
            var entry = artifact.AsSpan(envelopeBytes + (index * entryBytes), entryBytes);
            var offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]));
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
            counts.Add(BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]));
            versions.Add(BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]));
            sections.Add(artifact.AsSpan(offset, length).ToArray());
        }

        var reader = sections[5].AsSpan();
        var normalized = new List<byte>();
        var position = 0;
        for (var index = 0; index < counts[5]; index++)
        {
            normalized.AddRange(reader.Slice(position, 3).ToArray());
            position += 3;
            var nodeLength = reader[position];
            normalized.AddRange(reader.Slice(position, nodeLength + 1).ToArray());
            position += nodeLength + 1;
            var labelLength = reader[position];
            position += labelLength + 1 + 24;
            var groupLength = reader[position];
            position += groupLength + 1;
        }

        sections[5] = normalized.ToArray();
        var output = new List<byte>(artifact.AsSpan(0, envelopeBytes).ToArray());
        var sectionOffset = envelopeBytes + (8 * entryBytes);
        for (var index = 0; index < sections.Count; index++)
        {
            var entry = new byte[entryBytes];
            BinaryPrimitives.WriteUInt16LittleEndian(entry, checked((ushort)(index + 1)));
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(2), checked((ushort)(index == 5 ? 1 : versions[index])));
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4), checked((uint)sectionOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(8), checked((uint)sections[index].Length));
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(12), counts[index]);
            SHA256.HashData(sections[index]).CopyTo(entry, 16);
            output.AddRange(entry);
            sectionOffset += sections[index].Length;
        }

        foreach (var section in sections) output.AddRange(section);
        BinaryPrimitives.WriteUInt32LittleEndian(CollectionsMarshal.AsSpan(output).Slice(8, 4), checked((uint)output.Count));
        return output.ToArray();
    }

    private static byte[] Artifact(string fixture) =>
        File.ReadAllBytes(Path.Combine(FixtureRoot, fixture, "artifact.bin"));

    private static FlowCompilationRequest CompilationRequest(ExecutableFlowSource source) => new()
    {
        Source = source,
        Target = new FlowCompilationTarget
        {
            ControllerTemplate = new ValidatedControllerTemplate(
                new ControllerTemplate
                {
                    Id = source.ControllerTemplateId,
                    Name = "Recovered target",
                    Revision = checked((int)source.ControllerTemplateRevision)
                },
                new HashSet<PointValueType> { PointValueType.Digital, PointValueType.Analog },
                new HashSet<PointDirection> { PointDirection.Input, PointDirection.Output },
                new HashSet<ControllerPointFeature>(),
                new HashSet<ConnectorDataType> { ConnectorDataType.Boolean, ConnectorDataType.Number },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<ExecutionMode>(),
                new HashSet<ControllerRuntimeFeature>()),
            Points = source.Nodes
                .Where(node => node.Kind is "digitalInput" or "digitalOutput" or "analogInput" or "analogOutput")
                .Select(node => new Point
                {
                    Id = node.Configuration["pointId"].GetString()!,
                    Name = node.Configuration["pointId"].GetString()!,
                    Enabled = true,
                    Implementation = "virtual",
                    Direction = node.Kind.EndsWith("Input", StringComparison.Ordinal) ? "input" : "output",
                    ValueType = node.Kind.StartsWith("analog", StringComparison.Ordinal) ? "analog" : "digital",
                    Units = node.Kind.StartsWith("analog", StringComparison.Ordinal) ? "degC" : null,
                    Readable = node.Kind.EndsWith("Input", StringComparison.Ordinal),
                    Commandable = node.Kind.EndsWith("Output", StringComparison.Ordinal),
                    Persistence = "volatile",
                    Revision = 1
                })
                .DistinctBy(point => point.Id, StringComparer.Ordinal)
                .ToArray()
        }
    };
}

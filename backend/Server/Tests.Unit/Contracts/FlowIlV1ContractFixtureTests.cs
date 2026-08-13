using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Contracts;

public sealed class FlowIlV1ContractFixtureTests
{
    private const int EnvelopeLength = 128;
    private const int DirectoryEntryLength = 48;
    private const int SectionCount = 8;
    private const int SlotSectionId = 3;
    private const int InstructionSectionId = 4;
    private const int InstructionRecordLength = 12;
    private const ushort UnusedIndex = ushort.MaxValue;

    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    /// <summary>
    /// Verifies every fixture digest and the independent managed decoder's stable result.
    /// </summary>
    [Test]
    public void ManifestDigestsAndValidationResultsAreStable()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json")));
        var fixtures = manifest.RootElement.GetProperty("fixtures").EnumerateArray().ToArray();

        Assert.That(fixtures, Has.Length.EqualTo(12));
        foreach (var fixture in fixtures)
        {
            var id = fixture.GetProperty("id").GetString()!;
            var artifact = File.ReadAllBytes(Path.Combine(FixtureRoot, id, "artifact.bin"));
            var expected = fixture.GetProperty("expected");
            var result = DecodeMetadata(artifact);

            Assert.Multiple(() =>
            {
                Assert.That(artifact, Has.Length.EqualTo(fixture.GetProperty("artifactLength").GetInt32()));
                Assert.That(Convert.ToHexStringLower(SHA256.HashData(artifact)),
                    Is.EqualTo(fixture.GetProperty("artifactSha256").GetString()));
                Assert.That(result.Reason, Is.EqualTo(expected.GetProperty("reason").GetString()), id);
                Assert.That(result.Path, Is.EqualTo(expected.GetProperty("path").GetString()), id);
            });
        }
    }

    /// <summary>
    /// Verifies canonical metadata, maximum counts, and byte identity after source permutation.
    /// </summary>
    [Test]
    public void ValidMetadataMatchesReferenceCompilerOutput()
    {
        var canonical = File.ReadAllBytes(Path.Combine(FixtureRoot, "valid-two-button-and", "artifact.bin"));
        var permuted = File.ReadAllBytes(Path.Combine(FixtureRoot, "valid-source-order-permutation", "artifact.bin"));
        var metadata = DecodeMetadata(canonical);
        var maximum = DecodeMetadata(File.ReadAllBytes(Path.Combine(FixtureRoot, "maximum-boolean", "artifact.bin")));

        Assert.Multiple(() =>
        {
            Assert.That(permuted, Is.EqualTo(canonical));
            Assert.That(metadata.Reason, Is.EqualTo("ok"));
            Assert.That(metadata.FlowId, Is.EqualTo("two-button-and"));
            Assert.That(metadata.FlowRevision, Is.EqualTo(7));
            Assert.That(metadata.SectionCount, Is.EqualTo(SectionCount));
            Assert.That(metadata.InstructionCount, Is.EqualTo(5));
            Assert.That(metadata.SlotCount, Is.EqualTo(4));
            Assert.That(maximum.Reason, Is.EqualTo("ok"));
            Assert.That(maximum.InstructionCount, Is.EqualTo(129));
            Assert.That(maximum.SlotCount, Is.EqualTo(128));
        });
    }

    /// <summary>
    /// Decodes bounded envelope and directory metadata without using production compiler code.
    /// </summary>
    private static DecodeResult DecodeMetadata(byte[] artifact)
    {
        if (artifact.Length < EnvelopeLength || !artifact.AsSpan(0, 4).SequenceEqual("FIL1"u8) ||
            ReadUInt16(artifact, 4) != 1 || ReadUInt16(artifact, 6) != EnvelopeLength)
        {
            return DecodeResult.Error("malformed", "");
        }

        if (ReadUInt32(artifact, 8) != artifact.Length)
        {
            return DecodeResult.Error("length_mismatch", "/artifactLength");
        }

        var sectionCount = ReadUInt16(artifact, 26);
        if (sectionCount != SectionCount || ReadUInt32(artifact, 116) != EnvelopeLength ||
            EnvelopeLength + (sectionCount * DirectoryEntryLength) > artifact.Length)
        {
            return DecodeResult.Error("malformed", "");
        }

        var expectedOffset = EnvelopeLength + (sectionCount * DirectoryEntryLength);
        var instructionOffset = 0;
        var instructionLength = 0;
        var instructionCount = 0;
        var slotCount = 0;

        for (var index = 0; index < sectionCount; index++)
        {
            var entryOffset = EnvelopeLength + (index * DirectoryEntryLength);
            var id = ReadUInt16(artifact, entryOffset);
            if (id is < 1 or > SectionCount)
            {
                return DecodeResult.Error("unknown_section", $"/sections/{index}/id");
            }

            if (id != index + 1)
            {
                return DecodeResult.Error("non_canonical_order", $"/sections/{index}/id");
            }

            var offset = checked((int)ReadUInt32(artifact, entryOffset + 4));
            var length = checked((int)ReadUInt32(artifact, entryOffset + 8));
            var count = checked((int)ReadUInt32(artifact, entryOffset + 12));
            var version = ReadUInt16(artifact, entryOffset + 2);
            if (version != 1 || offset != expectedOffset ||
                length < 0 || offset < 0 || offset > artifact.Length || length > artifact.Length - offset)
            {
                return DecodeResult.Error("malformed", "");
            }

            var digest = SHA256.HashData(artifact.AsSpan(offset, length));
            if (!digest.AsSpan().SequenceEqual(artifact.AsSpan(entryOffset + 16, digest.Length)))
            {
                return DecodeResult.Error("malformed", "");
            }

            expectedOffset += length;
            if (id == SlotSectionId)
            {
                slotCount = count;
            }

            if (id == InstructionSectionId)
            {
                instructionOffset = offset;
                instructionLength = length;
                instructionCount = count;
            }
        }

        if (expectedOffset != artifact.Length || instructionLength != instructionCount * InstructionRecordLength)
        {
            return DecodeResult.Error("malformed", "");
        }

        for (var index = 0; index < instructionCount; index++)
        {
            var offset = instructionOffset + (index * InstructionRecordLength);
            var result = ReadUInt16(artifact, offset + 2);
            var operand0 = ReadUInt16(artifact, offset + 4);
            var operand1 = ReadUInt16(artifact, offset + 6);
            if ((result != UnusedIndex && result >= slotCount) ||
                (operand0 != UnusedIndex && operand0 >= slotCount) ||
                (operand1 != UnusedIndex && operand1 >= slotCount))
            {
                var field = result != UnusedIndex && result >= slotCount ? "resultSlot" : "operand";
                return DecodeResult.Error("invalid_operand", $"/instructions/{index}/{field}");
            }
        }

        var flowIdLength = artifact[52];
        if (flowIdLength is 0 or > 63)
        {
            return DecodeResult.Error("malformed", "");
        }

        return new DecodeResult(
            "ok",
            string.Empty,
            Encoding.UTF8.GetString(artifact, 53, flowIdLength),
            checked((int)ReadUInt32(artifact, 16)),
            sectionCount,
            instructionCount,
            slotCount);
    }

    /// <summary>
    /// Reads a little-endian unsigned 16-bit integer from a checked fixture offset.
    /// </summary>
    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    /// <summary>
    /// Reads a little-endian unsigned 32-bit integer from a checked fixture offset.
    /// </summary>
    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
    }

    private sealed record DecodeResult(
        string Reason,
        string Path,
        string FlowId,
        int FlowRevision,
        int SectionCount,
        int InstructionCount,
        int SlotCount)
    {
        /// <summary>
        /// Creates a failed decode result without inventing metadata values.
        /// </summary>
        public static DecodeResult Error(string reason, string path)
        {
            return new DecodeResult(reason, path, string.Empty, 0, 0, 0, 0);
        }
    }
}

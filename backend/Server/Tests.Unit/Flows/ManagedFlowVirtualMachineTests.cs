using Server.Services;
using Server.Services.Contracts;
using Server.Services.Extensions;
using Server.Services.Implementation;

namespace Tests.Unit.Flows;

public sealed class ManagedFlowVirtualMachineTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, true, true)]
    public void ExecutesBooleanFlowWithoutNativeCode(bool first, bool second, bool expected)
    {
        using var machine = Machine("valid-two-button-and");

        var result = machine.Scan(
            [new("input-01", first), new("input-08", second)],
            sampledAtMilliseconds: 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.ScanNumber, Is.EqualTo(1));
            Assert.That(result.Commands, Has.Count.EqualTo(1));
            Assert.That(result.Commands[0].PointId, Is.EqualTo("output-01"));
            Assert.That(result.Commands[0].TypedValue.Boolean, Is.EqualTo(expected));
        });
    }

    [Test]
    public void PreservesStateAcrossScansAndReset()
    {
        using var machine = Machine("valid-memory-feedback");

        var first = machine.Scan([], 1);
        var second = machine.Scan([], 2);

        machine.Reset();

        var reset = machine.Scan([], 3);

        Assert.Multiple(() =>
        {
            Assert.That(first.Commands.Single().TypedValue.Number, Is.EqualTo(2));
            Assert.That(second.Commands.Single().TypedValue.Number, Is.EqualTo(2));
            Assert.That(reset.Commands.Single().TypedValue.Number, Is.EqualTo(2));
            Assert.That(reset.ScanNumber, Is.EqualTo(1));
        });
    }

    [Test]
    public void SupportsInstructionSteppingAndAbort()
    {
        using var machine = Machine("valid-two-button-and");

        var initial = machine.BeginScan([new("input-01", true), new("input-08", true)], 1);
        var stepped = machine.StepInstruction();
        machine.AbortScan();
        var completed = machine.Scan([new("input-01", true), new("input-08", true)], 2);

        Assert.Multiple(() =>
        {
            Assert.That(initial.InstructionIndex, Is.Zero);
            Assert.That(stepped.InstructionIndex, Is.EqualTo(1));
            Assert.That(completed.Commands.Single().TypedValue.Boolean, Is.True);
        });
    }

    [Test]
    public void RejectsBadQualityWhenArtifactRequiresGoodInputs()
    {
        using var machine = Machine("valid-two-button-and");

        var action = () => machine.Scan(
            [new("input-01", FlowVmValue.FromBoolean(true, DataQualityExtensions.Bad)), new("input-08", true)],
            1);

        Assert.That(action, Throws.TypeOf<FlowVirtualMachineException>()
            .With.Property(nameof(FlowVirtualMachineException.Code)).EqualTo(17));
    }

    [Test]
    public void ExecutesTypedNumericLanguage()
    {
        using var machine = Machine("valid-numeric-language");

        var result = machine.Scan([], 1);

        Assert.That(result.Slots.Last().Boolean, Is.True);
    }

    private static IFlowVirtualMachine Machine(string fixture) =>
        new ManagedFlowVirtualMachineFactory().Create(
            File.ReadAllBytes(Path.Combine(FixtureRoot, fixture, "artifact.bin")));
}
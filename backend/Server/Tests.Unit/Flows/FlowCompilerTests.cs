using Server.Common.Contracts;
using Server.Compiler;
using Server.Compiler.Contracts;
using Server.Compiler.Extensions;
using Server.Compiler.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;
using Tests.Unit.Helpers;

namespace Tests.Unit.Flows;

public sealed class FlowCompilerTests
{
    private ServiceProvider _serviceProvider = null!;
    private IFlowCompiler _compiler = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        services.AddFlowCompilerServices();

        _serviceProvider = services.BuildServiceProvider();
        _compiler = _serviceProvider.GetRequiredService<IFlowCompiler>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider.Dispose();
    }

    private static readonly FlowNodeKind[] FlowFunctionKinds =
    [
        FlowNodeKind.A2D, FlowNodeKind.Add, FlowNodeKind.Subtract, FlowNodeKind.Multiply, FlowNodeKind.Divide, FlowNodeKind.Power, FlowNodeKind.Negate, FlowNodeKind.AnalogInput, FlowNodeKind.AnalogOutput, FlowNodeKind.And, FlowNodeKind.Average, FlowNodeKind.Calculator, FlowNodeKind.Calendar,
        FlowNodeKind.Clamp, FlowNodeKind.Comparator, FlowNodeKind.Delay, FlowNodeKind.DigitalConstant, FlowNodeKind.DigitalInput, FlowNodeKind.DigitalOutput,
        FlowNodeKind.DigitalSwitch, FlowNodeKind.Line, FlowNodeKind.LevelShifter, FlowNodeKind.Max, FlowNodeKind.Memory, FlowNodeKind.Min,
        FlowNodeKind.Nand, FlowNodeKind.Nor, FlowNodeKind.Not, FlowNodeKind.NumericConstant, FlowNodeKind.OnDelay, FlowNodeKind.Or, FlowNodeKind.Override, FlowNodeKind.Pulse,
        FlowNodeKind.QualityGood, FlowNodeKind.RisingEdge, FlowNodeKind.Schedule, FlowNodeKind.AnalogSwitch, FlowNodeKind.Sequence, FlowNodeKind.Split ,FlowNodeKind.Timer,
        FlowNodeKind.D2A, FlowNodeKind.Xnor, FlowNodeKind.Xor, FlowNodeKind.Counter
    ];

    private static readonly string FixtureSourceRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    private static readonly string FixtureExpectedRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    private static readonly string FixtureUpdateRoot = Path.Combine(
        FixtureUpdater.WorkspaceDirectory(),
        "testdata",
        "contracts",
        "flow-il-v1");

    private static ExecutableFlowSource GetSourceFromKind(FlowNodeKind kind)
    {
        var numericInputs = kind switch
        {
            FlowNodeKind.Add or FlowNodeKind.Subtract or FlowNodeKind.Multiply or FlowNodeKind.Divide or FlowNodeKind.Power or FlowNodeKind.Average or FlowNodeKind.Comparator or FlowNodeKind.Min or FlowNodeKind.Max => ["a", "b"],
            FlowNodeKind.AnalogSwitch => ["a", "b"],
            FlowNodeKind.Calculator => ["a", "b", "c"],
            FlowNodeKind.Clamp or FlowNodeKind.Line or FlowNodeKind.Split => new[] { "input" },
            FlowNodeKind.LevelShifter or FlowNodeKind.A2D or FlowNodeKind.Negate => ["in"],
            FlowNodeKind.AnalogOutput => ["in"],
            FlowNodeKind.Memory or FlowNodeKind.QualityGood => ["in"],
            _ => []
        };

        string[] booleanInputs = kind switch
        {
            FlowNodeKind.Not or FlowNodeKind.OnDelay or FlowNodeKind.RisingEdge => ["in"],
            FlowNodeKind.Counter => ["count", "reset"],
            FlowNodeKind.And or FlowNodeKind.Or or FlowNodeKind.Nand or FlowNodeKind.Nor or FlowNodeKind.Xnor or FlowNodeKind.Xor or FlowNodeKind.Sequence => ["a", "b"],
            FlowNodeKind.DigitalSwitch => ["condition", "whenTrue", "whenFalse"],
            FlowNodeKind.AnalogSwitch => ["condition"],
            FlowNodeKind.Override or FlowNodeKind.Delay or FlowNodeKind.Timer or FlowNodeKind.Pulse => ["input"],
            FlowNodeKind.DigitalOutput or FlowNodeKind.D2A => ["in"],
            _ => []
        };

        var configuration = kind switch
        {
            FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput =>
                Config("pointId", "test-point"),
            FlowNodeKind.DigitalConstant =>
                Config("value", true),
            FlowNodeKind.NumericConstant or FlowNodeKind.Memory =>
                Config("value", 1D),
            FlowNodeKind.Comparator =>
                Config("operator", "gt"),
            FlowNodeKind.Calculator =>
                Config("formula", "a * b + c"),
            FlowNodeKind.LevelShifter or FlowNodeKind.Line =>
                Config(("gain", 1D), ("offset", 0D)),
            FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer or FlowNodeKind.Pulse =>
                Config("durationMs", 100D),
            FlowNodeKind.Clamp => Config(("minimum", 0D), ("maximum", 100D)),
            FlowNodeKind.A2D => Config(("activeLowThreshold", 25D), ("activeHighThreshold", 75D)),
            FlowNodeKind.D2A => Config(("lowValue", 0D), ("highValue", 100D)),
            FlowNodeKind.Schedule or FlowNodeKind.Calendar =>
                Config("enabled", true),
            _ => []
        };

        var nodes = new List<ExecutableFlowNode>();
        var connections = new List<ExecutableFlowConnection>();

        foreach (var port in numericInputs)
        {
            var id = $"number-{port}";
            nodes.Add(new ExecutableFlowNode { Id = id, Kind = FlowNodeKind.NumericConstant, Configuration = Config("value", 1D) });
            connections.Add(new ExecutableFlowConnection(new ExecutableFlowEndpoint(id, "value"), new ExecutableFlowEndpoint("test-node", port)));
        }

        foreach (var port in booleanInputs)
        {
            var id = $"boolean-{port}";
            nodes.Add(new ExecutableFlowNode { Id = id, Kind = FlowNodeKind.DigitalConstant, Configuration = Config("value", true) });
            connections.Add(new ExecutableFlowConnection(new ExecutableFlowEndpoint(id, "value"), new ExecutableFlowEndpoint("test-node", port)));
        }

        nodes.Add(new ExecutableFlowNode { Id = "test-node", Kind = kind, Configuration = configuration });

        return new ExecutableFlowSource
        {
            Id = $"test-{kind}",
            Revision = 1,
            ControllerTemplateId = "fixture",
            ControllerTemplateRevision = 1,
            Nodes = nodes,
            Connections = connections
        };
    }

    /// <summary>
    /// Purpose: Ensures every executable function has a compiler-valid ordinary flow fixture.
    /// Description: Builds a minimal fully-driven graph for each canonical kind and compiles it through Flow IL.
    /// </summary>
    [TestCaseSource(nameof(FlowFunctionKinds))]
    public void EveryExecutableTutorialKindCompilesThroughTheNormalCompiler(FlowNodeKind kind)
    {
        // Arrange: Create a current-schema fixture with typed constant drivers and canonical configuration.
        var source = GetSourceFromKind(kind);

        // Act: Compile through the production compiler rather than test-specific semantics.
        var compilationRequest = BuildCompilationRequest(source);
        var compilation = _compiler.Compile(compilationRequest);

        // Assert: A bounded current artifact and stable source identity are produced.
        Assert.Multiple(() =>
        {
            Assert.That(compilation.Artifact.Length, Is.GreaterThan(0));
            Assert.That(compilation.Artifact.Length, Is.LessThanOrEqualTo(16_384));
            Assert.That(compilation.NodeIndices, Does.ContainKey("test-node"));
        });
    }

    [TestCase("valid-two-button-and")]
    [TestCase("valid-source-order-permutation")]
    [TestCase("valid-memory-feedback")]
    public void CompilesGoldenSourceToTheExactCanonicalArtifact(string fixture)
    {
        var result = CompileFixture(fixture);

        var sourceRoot = FixtureUpdater.IsEnabled(fixture)
            ? FixtureUpdateRoot
            : FixtureExpectedRoot;

        var expected = File.ReadAllBytes(
            Path.Combine(sourceRoot, fixture, "artifact.bin"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Artifact.ToArray(), Is.EqualTo(expected));
            Assert.That(result.ArtifactSha256,
                Is.EqualTo(Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(expected))));
            Assert.That(result.ArtifactVersion, Is.EqualTo(1));
            Assert.That(result.NodeIndices.Keys, Is.EqualTo(result.Schedule));
        });
    }

    [TestCase(FlowNodeKind.Nand, FlowOpcode.Nand)]
    [TestCase(FlowNodeKind.Nor, FlowOpcode.Nor)]
    [TestCase(FlowNodeKind.Xor, FlowOpcode.Xor)]
    [TestCase(FlowNodeKind.Xnor, FlowOpcode.Xnor)]
    public void LowersExpandedBooleanNodesToTheirNormativeOpcode(FlowNodeKind kind, FlowOpcode opcode)
    {
        const string fixture = "valid-two-button-and";

        var source = ReadSource(fixture);
        source = source with
        {
            Nodes = [.. source.Nodes.Select(node =>
            node.Kind == FlowNodeKind.And
                ? node with { Kind = kind }
                : node)]
        };

        var compilationRequest = BuildCompilationRequest(source);
        var artifact = _compiler
            .Compile(compilationRequest)
            .Artifact
            .ToArray();

        var instructionSection =
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                artifact.AsSpan(128 + (3 * 48) + 4, 4));

        var instructionCount =
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                artifact.AsSpan(32, 4));

        Assert.That(
            Enumerable.Range(0, checked((int)instructionCount))
                .Select(index =>
                    artifact[checked((int)instructionSection) + (index * 12)]),
            Does.Contain((byte)opcode));
    }

    [Test]
    public void EncodesVirtualPointsWithTheirDistinctControllerBindingKind()
    {
        var source = ReadSource("valid-two-button-and");
        var request = BuildCompilationRequest(source);
        request = request with
        {
            Target = request.Target with
            {
                Points = [.. request.Target.Points.Select(point => point with { Implementation = "virtual" })]
            }
        };
        var artifact = _compiler.Compile(request).Artifact.ToArray();
        var pointSection = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
            artifact.AsSpan(128 + 48 + 4, 4));

        Assert.That(artifact[checked((int)pointSection) + 3], Is.EqualTo((byte)PointBindingKind.VirtualPoint));
    }

    [Test]
    public void RejectsUnsupportedNodesWithAStableGraphPath()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Nodes =
            [
                ReadSource("valid-two-button-and").Nodes[0] with { Kind = FlowNodeKind.Unknown }
            ],
            Connections = []
        };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.UnsupportedNode,
            "/nodes/0/kind");
    }

    [Test]
    public void RejectsCombinationalCyclesWithTheLexicallyFirstNodePath()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Nodes =
            [
                new ExecutableFlowNode { Id = "not-a", Kind = FlowNodeKind.Not },
                new ExecutableFlowNode { Id = "not-b", Kind = FlowNodeKind.Not }
            ],
            Connections =
            [
                new ExecutableFlowConnection(new ExecutableFlowEndpoint("not-a", "value"), new ExecutableFlowEndpoint("not-b", "in")),
                new ExecutableFlowConnection(new ExecutableFlowEndpoint("not-b", "value"), new ExecutableFlowEndpoint("not-a", "in"))
            ]
        };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.CombinationalCycle,
            "/nodes/not-a");
    }

    [Test]
    public void RejectsMissingInputDriversBeforeEncoding()
    {
        var source = ReadSource("valid-two-button-and") with { Connections = [] };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.MissingInputDriver,
            "/nodes/and-main/ports/a");
    }

    [Test]
    public void ReportsScheduledResourceRequirementsForThePlcScan()
    {
        var result = CompileFixture("valid-memory-feedback");

        Assert.Multiple(() =>
        {
            Assert.That(result.Schedule, Is.EqualTo(new[]
            {
                "constant-2",
                "memory-1",
                "output-01-node"
            }));
            Assert.That(result.MaximumWorkPerScan, Is.EqualTo(5));
            Assert.That(result.WorkingBytes, Is.EqualTo(128));
            Assert.That(result.MaximumSnapshotBytes, Is.EqualTo(16384));
        });
    }

    [TestCase(0)]
    [TestCase(99)]
    public void RejectsEveryNonCurrentArtifactVersionWithAStablePath(int artifactVersion)
    {
        var request = BuildCompilationRequest(ReadSource("valid-two-button-and")) with { ArtifactVersion = artifactVersion };

        AssertDiagnostic(
            () => _compiler.Compile(request),
            FlowCompilationDiagnosticCode.UnsupportedArtifactVersion,
            "/artifactVersion");
    }

    [Test]
    public void CapturesResolvedPointRevisionsInTheCanonicalArtifact()
    {
        var source = ReadSource("valid-two-button-and");
        var firstRequest = BuildCompilationRequest(source);
        var first = _compiler.Compile(firstRequest);
        var changedPoint = firstRequest.Target.Points[0] with { Revision = 2 };
        var second = _compiler.Compile(firstRequest with
        {
            Target = firstRequest.Target with
            {
                Points = [changedPoint, .. firstRequest.Target.Points.Skip(1)]
            }
        });

        Assert.That(second.Artifact.ToArray(), Is.Not.EqualTo(first.Artifact.ToArray()));
    }

    [Test]
    public void RejectsAnUnresolvedPointDependencyBeforeEmission()
    {
        var source = ReadSource("valid-two-button-and");
        var request = BuildCompilationRequest(source);
        request = request with { Target = request.Target with { Points = [.. request.Target.Points.Skip(1)] } };

        AssertDiagnostic(
            () => _compiler.Compile(request),
            FlowCompilationDiagnosticCode.MissingPoint,
            $"/points/{source.Nodes[0].Configuration["pointId"].GetString()}");
    }

    [Test]
    public void CalculatorCompilesBodmasFormulaIntoArithmeticInstructionsAndTemporarySlots()
    {
        var source = GetSourceFromKind(FlowNodeKind.Calculator);
        var calculator = source.Nodes.Single(node => node.Id == "test-node") with
        {
            Configuration = Config("formula", "a + b * (c - a) ^ b")
        };
        source = source with { Nodes = [.. source.Nodes.Where(node => node.Id != "test-node"), calculator] };

        var result = _compiler.Compile(BuildCompilationRequest(source));

        Assert.Multiple(() =>
        {
            Assert.That(result.InstructionCount, Is.EqualTo(10));
            Assert.That(result.SlotCount, Is.EqualTo(8));
            Assert.That(result.NodeIndices["test-node"], Is.LessThan(result.SlotCount));
        });
    }

    [Test]
    public void DivideExecutesWithBothNumericOperands()
    {
        var source = GetSourceFromKind(FlowNodeKind.Divide);
        source = source with
        {
            Nodes = [.. source.Nodes.Select(node => node.Id switch
            {
                "number-a" => node with { Configuration = Config("value", 9D) },
                "number-b" => node with { Configuration = Config("value", 5D) },
                _ => node
            })]
        };
        var compilation = _compiler.Compile(BuildCompilationRequest(source));
        using var machine = new ManagedFlowVirtualMachineFactory().Create(compilation.Artifact);

        var scan = machine.Scan([], 1);

        Assert.That(scan.Slots[compilation.NodeIndices["test-node"]].Number, Is.EqualTo(1.8));
    }

    [Test]
    public void DelayPostponesBothBooleanStateChangesForTheConfiguredDuration()
    {
        var source = GetSourceFromKind(FlowNodeKind.Delay);
        source = source with
        {
            Nodes =
            [
                .. source.Nodes.Select(node => node.Id switch
                {
                    "boolean-input" => node with
                    {
                        Kind = FlowNodeKind.DigitalInput,
                        Configuration = Config("pointId", "input")
                    },
                    "test-node" => node with { Configuration = Config("durationMs", 2_000D) },
                    _ => node
                }),
                new ExecutableFlowNode
                {
                    Id = "output",
                    Kind = FlowNodeKind.DigitalOutput,
                    Configuration = Config("pointId", "output")
                }
            ],
            Connections =
            [
                .. source.Connections,
                new(new("test-node", "output"), new("output", "in"))
            ]
        };
        var compilation = _compiler.Compile(BuildCompilationRequest(source));
        using var machine = new ManagedFlowVirtualMachineFactory().Create(compilation.Artifact);

        bool Scan(bool input, ulong sampledAt) => machine
            .Scan([new("input", input)], sampledAt)
            .Commands.Single()
            .TypedValue.Boolean;

        Assert.Multiple(() =>
        {
            Assert.That(Scan(false, 0), Is.False);
            Assert.That(Scan(true, 0), Is.False);
            Assert.That(Scan(true, 1_999), Is.False);
            Assert.That(Scan(true, 2_000), Is.True);
            Assert.That(Scan(false, 2_000), Is.True);
            Assert.That(Scan(false, 3_999), Is.True);
            Assert.That(Scan(false, 4_000), Is.False);
        });
    }

    [Test]
    public void DivideExecutesWithAnalogPointInputsAndOutput()
    {
        var source = GetSourceFromKind(FlowNodeKind.Divide);
        source = source with
        {
            Nodes =
            [
                .. source.Nodes.Select(node => node.Id switch
                {
                    "number-a" => node with { Kind = FlowNodeKind.AnalogInput, Configuration = Config("pointId", "input-a") },
                    "number-b" => node with { Kind = FlowNodeKind.AnalogInput, Configuration = Config("pointId", "input-b") },
                    _ => node
                }),
                new ExecutableFlowNode
                {
                    Id = "output",
                    Kind = FlowNodeKind.AnalogOutput,
                    Configuration = Config("pointId", "output")
                }
            ],
            Connections =
            [
                .. source.Connections,
                new ExecutableFlowConnection(
                    new ExecutableFlowEndpoint("test-node", "value"),
                    new ExecutableFlowEndpoint("output", "in"))
            ]
        };
        var compilation = _compiler.Compile(BuildCompilationRequest(source));
        using var machine = new ManagedFlowVirtualMachineFactory().Create(compilation.Artifact);

        var scan = machine.Scan(
            [new FlowVmInput("input-a", FlowVmValue.FromNumber(9)), new FlowVmInput("input-b", FlowVmValue.FromNumber(5))],
            1);

        Assert.That(scan.Commands.Single().TypedValue.Number, Is.EqualTo(1.8));
    }

    [Test]
    public void DivideByZeroHoldsLastGoodValueAndRaisesErrorOutput()
    {
        var source = GetSourceFromKind(FlowNodeKind.Divide) with
        {
            Nodes =
            [
                .. GetSourceFromKind(FlowNodeKind.Divide).Nodes.Select(node => node.Id switch
                {
                    "number-a" => node with { Kind = FlowNodeKind.AnalogInput, Configuration = Config("pointId", "input-a") },
                    "number-b" => node with { Kind = FlowNodeKind.AnalogInput, Configuration = Config("pointId", "input-b") },
                    _ => node
                }),
                new ExecutableFlowNode { Id = "value-output", Kind = FlowNodeKind.AnalogOutput, Configuration = Config("pointId", "value-output") },
                new ExecutableFlowNode { Id = "error-output", Kind = FlowNodeKind.DigitalOutput, Configuration = Config("pointId", "error-output") }
            ],
            Connections =
            [
                .. GetSourceFromKind(FlowNodeKind.Divide).Connections,
                new(new("test-node", "value"), new("value-output", "in")),
                new(new("test-node", "error"), new("error-output", "in"))
            ]
        };
        var compilation = _compiler.Compile(BuildCompilationRequest(source));
        using var machine = new ManagedFlowVirtualMachineFactory().Create(compilation.Artifact);

        var good = machine.Scan([new("input-a", FlowVmValue.FromNumber(9)), new("input-b", FlowVmValue.FromNumber(3))], 1);
        var failed = machine.Scan([new("input-a", FlowVmValue.FromNumber(9)), new("input-b", FlowVmValue.FromNumber(0))], 2);
        var overflow = machine.Scan([new("input-a", FlowVmValue.FromNumber(double.MaxValue)), new("input-b", FlowVmValue.FromNumber(0.5))], 3);
        var recovered = machine.Scan([new("input-a", FlowVmValue.FromNumber(-12)), new("input-b", FlowVmValue.FromNumber(3))], 4);

        Assert.Multiple(() =>
        {
            Assert.That(good.Commands.Single(command => command.PointId == "value-output").TypedValue.Number, Is.EqualTo(3));
            Assert.That(good.Commands.Single(command => command.PointId == "error-output").TypedValue.Boolean, Is.False);
            Assert.That(failed.Commands.Single(command => command.PointId == "value-output").TypedValue.Number, Is.EqualTo(3));
            Assert.That(failed.Commands.Single(command => command.PointId == "error-output").TypedValue.Boolean, Is.True);
            Assert.That(overflow.Commands.Single(command => command.PointId == "value-output").TypedValue.Number, Is.EqualTo(3));
            Assert.That(overflow.Commands.Single(command => command.PointId == "error-output").TypedValue.Boolean, Is.True);
            Assert.That(recovered.Commands.Single(command => command.PointId == "value-output").TypedValue.Number, Is.EqualTo(-4));
            Assert.That(recovered.Commands.Single(command => command.PointId == "error-output").TypedValue.Boolean, Is.False);
        });
    }

    [Test]
    public void PulseRemainsOnForTheConfiguredDurationAndRequiresAnotherRisingEdge()
    {
        var source = GetSourceFromKind(FlowNodeKind.Pulse);
        source = source with
        {
            Nodes =
            [
                .. source.Nodes.Select(node => node.Id switch
                {
                    "boolean-input" => node with
                    {
                        Kind = FlowNodeKind.DigitalInput,
                        Configuration = Config("pointId", "input")
                    },
                    "test-node" => node with { Configuration = Config("durationMs", 2_000D) },
                    _ => node
                }),
                new ExecutableFlowNode
                {
                    Id = "output",
                    Kind = FlowNodeKind.DigitalOutput,
                    Configuration = Config("pointId", "output")
                }
            ],
            Connections =
            [
                .. source.Connections,
                new(new("test-node", "output"), new("output", "in"))
            ]
        };
        var compilation = _compiler.Compile(BuildCompilationRequest(source));
        using var machine = new ManagedFlowVirtualMachineFactory().Create(compilation.Artifact);

        bool Scan(bool input, ulong sampledAt) => machine
            .Scan([new("input", input)], sampledAt)
            .Commands.Single()
            .TypedValue.Boolean;

        Assert.Multiple(() =>
        {
            Assert.That(Scan(false, 0), Is.False);
            Assert.That(Scan(true, 0), Is.True);
            Assert.That(Scan(true, 1_999), Is.True);
            Assert.That(Scan(true, 2_000), Is.False);
            Assert.That(Scan(true, 3_000), Is.False);
            Assert.That(Scan(false, 3_000), Is.False);
            Assert.That(Scan(true, 3_000), Is.True);
        });
    }

    [TestCase("a * 9 + c")]
    [TestCase("a + temperature")]
    [TestCase("a / (b - b")]
    public void CalculatorRejectsLiteralsUnknownVariablesAndMalformedFormulas(string formula)
    {
        var source = GetSourceFromKind(FlowNodeKind.Calculator);
        var calculator = source.Nodes.Single(node => node.Id == "test-node") with
        {
            Configuration = Config("formula", formula)
        };
        source = source with { Nodes = [.. source.Nodes.Where(node => node.Id != "test-node"), calculator] };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.InvalidCalculatorFormula,
            "/nodes/3/configuration/formula");
    }
    private static Dictionary<string, JsonElement> Config(string key, object value) =>
        new() { [key] = JsonSerializer.SerializeToElement(value) };

    private static Dictionary<string, JsonElement> Config(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    private static ExecutableFlowSource ReadSource(string fixture)
    {
        var sourceRoot = FixtureUpdater.IsEnabled(fixture)
          ? FixtureUpdateRoot
          : FixtureSourceRoot;

        var json = File.ReadAllText(Path.Combine(sourceRoot, fixture, "source-flow.json"));
        return JsonSerializer.Deserialize<ExecutableFlowSource>(json, FlowControlJson.Options)!;
    }

    private static FlowCompilationRequest BuildCompilationRequest(ExecutableFlowSource source) => new()
    {
        Source = source,
        Target = new FlowCompilationTarget
        {
            ControllerTemplate = new ValidatedControllerTemplate(
                new ControllerTemplate
                {
                    Id = source.ControllerTemplateId,
                    Name = "Fixture target",
                    Revision = checked((int)source.ControllerTemplateRevision)
                },
                new HashSet<FlowPointValueType> { FlowPointValueType.Digital, FlowPointValueType.Analog },
                new HashSet<DataDirection> { DataDirection.Input, DataDirection.Output },
                new HashSet<ControllerPointFeature>(),
                new HashSet<ConnectorDataType> { ConnectorDataType.Boolean, ConnectorDataType.Number },
                new HashSet<FlowFunctionKind>(),
                new HashSet<ExecutionMode>(),
                new HashSet<ControllerRuntimeFeature>()),
            Points = [.. source.Nodes
                .Where(node => node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput)
                .Select(node => new FlowPoint
                {
                    Id = node.Configuration["pointId"].GetString()!,
                    Name = node.Configuration["pointId"].GetString()!,
                    Enabled = true,
                    Implementation = "bound",
                    Direction = node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.AnalogInput ? DataDirection.Input : DataDirection.Output,
                    ValueType = node.Kind is FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput ? FlowPointValueType.Analog : FlowPointValueType.Digital,
                    Readable = node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.AnalogInput,
                    Commandable = node.Kind is FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogOutput,
                    Persistence = "volatile",
                    Revision = 1
                })
                .DistinctBy(point => point.Id, StringComparer.Ordinal)]
        }
    };

    private static void AssertDiagnostic(TestDelegate action, FlowCompilationDiagnosticCode code, string path)
    {
        var exception = Assert.Throws<FlowCompilationException>(action);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostics[0].Code, Is.EqualTo(code));
            Assert.That(exception.Diagnostics[0].Path, Is.EqualTo(path));
        });
    }

    /// <summary>
    /// Compiles the specified flow source for a fixture and updates its generated
    /// artifacts when fixture regeneration is enabled.
    /// </summary>
    /// <param name="fixture">The name of the fixture.</param>
    /// <param name="source">The executable flow source to compile.</param>
    /// <returns>The compiled flow result.</returns>
    private FlowCompilationResult CompileFixture(
        string fixture,
        ExecutableFlowSource source)
    {
        var request = BuildCompilationRequest(source);
        var result = _compiler.Compile(request);

        FixtureUpdater.UpdateFlowCompilation(
            fixture,
            result,
            FixtureUpdateRoot,
            _compiler);

        return result;
    }

    /// <summary>
    /// Loads and compiles the specified flow fixture and updates its generated
    /// artifacts when fixture regeneration is enabled.
    /// </summary>
    /// <param name="fixture">The name of the fixture to load and compile.</param>
    /// <returns>The compiled flow result.</returns>
    private FlowCompilationResult CompileFixture(string fixture)
    {
        var source = ReadSource(fixture);
        return CompileFixture(fixture, source);
    }
}
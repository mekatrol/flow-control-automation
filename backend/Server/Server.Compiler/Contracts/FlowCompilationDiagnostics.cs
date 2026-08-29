using System.Collections.ObjectModel;
using System.Globalization;

namespace Server.Compiler.Contracts;

public static class FlowCompilationDiagnostics
{
    private static readonly ReadOnlyDictionary<FlowCompilationDiagnosticCode, FlowCompilationDiagnosticDefinition> Definitions =
        new(
            new Dictionary<FlowCompilationDiagnosticCode, FlowCompilationDiagnosticDefinition>
            {
                // Source/request
                [FlowCompilationDiagnosticCode.UnsupportedArtifactVersion] = D(1001, "Unsupported artifact version", "Only Flow IL version {0} is supported."),
                [FlowCompilationDiagnosticCode.UnsupportedSourceSchema] = D(1002, "Unsupported source schema", "Only source schema {0} is supported."),
                [FlowCompilationDiagnosticCode.InvalidFlowRevision] = D(1003, "Invalid flow revision", "Flow revision must be greater than zero."),
                [FlowCompilationDiagnosticCode.InvalidControllerTemplateRevision] = D(1004, "Invalid controller template revision", "Controller template revision must be greater than zero."),
                [FlowCompilationDiagnosticCode.UnsupportedExecution] = D(1005, "Unsupported execution configuration", "Source schema {0} supports manual execution with require-good or propagate input quality."),
                [FlowCompilationDiagnosticCode.NodeCountOutOfRange] = D(1006, "Invalid node count", "Node count must be between {0} and {1}."),
                [FlowCompilationDiagnosticCode.ConnectionCountLimitExceeded] = D(1007, "Connection limit exceeded", "Connection count exceeds {0}."),
                [FlowCompilationDiagnosticCode.ArtifactSizeLimitExceeded] = D(1008, "Artifact size limit exceeded", "Encoded Flow IL exceeds {0} bytes."),
                [FlowCompilationDiagnosticCode.InvalidIdentifier] = D(1009, "Invalid identifier", "Identifier has invalid syntax or exceeds the permitted encoded length."),
                [FlowCompilationDiagnosticCode.InvalidAuthoringMetadata] = D(1010, "Invalid authoring metadata", "Label or canvas metadata exceeds the supported bounds."),

                // Graph
                [FlowCompilationDiagnosticCode.DuplicateNode] = D(1101, "Duplicate node", "Node ID \"{0}\" is duplicated."),
                [FlowCompilationDiagnosticCode.UnsupportedNode] = D(1102, "Unsupported node", "Node kind \"{0}\" is unsupported."),
                [FlowCompilationDiagnosticCode.EndpointNotFound] = D(1103, "Endpoint not found", "The referenced connection endpoint does not exist."),
                [FlowCompilationDiagnosticCode.InvalidConnectionDirection] = D(1104, "Invalid connection direction", "A connection must run from an output port to an input port."),
                [FlowCompilationDiagnosticCode.ConnectionTypeMismatch] = D(1105, "Connection type mismatch", "Connected ports must use the same value type."),
                [FlowCompilationDiagnosticCode.DuplicateInputDriver] = D(1106, "Input already has a driver", "An input port may have only one driver."),
                [FlowCompilationDiagnosticCode.MissingInputDriver] = D(1107, "Input has no driver", "A required input port has no driver."),
                [FlowCompilationDiagnosticCode.CyclicDependency] = D(1108, "Cyclic dependency", "The flow contains a cyclic dependency and cannot be scheduled."),
                [FlowCompilationDiagnosticCode.CombinationalCycle] = D(1109, "Combinational cycle", "The graph contains a combinational cycle involving node \"{0}\"."),
                [FlowCompilationDiagnosticCode.DuplicatePointOutputDriver] = D(1110, "Point has multiple output drivers", "Only one proposed-output node may target point \"{0}\"."),

                // Configuration
                [FlowCompilationDiagnosticCode.MissingPointId] = D(1301, "Missing point ID", "A pointId string is required."),
                [FlowCompilationDiagnosticCode.UnexpectedPointConfigurationProperty] = D(1302, "Unsupported point configuration", "Only pointId and optional units are supported for this node."),
                [FlowCompilationDiagnosticCode.InvalidPointUnits] = D(1303, "Invalid point units", "Point units must be a string."),
                [FlowCompilationDiagnosticCode.InvalidBooleanConfiguration] = D(1304, "Invalid Boolean configuration", "A Boolean value is required."),
                [FlowCompilationDiagnosticCode.InvalidComparisonOperator] = D(1305, "Invalid comparison operator", "A supported comparison operator is required."),
                [FlowCompilationDiagnosticCode.InvalidGainOffsetConfiguration] = D(1306, "Invalid gain/offset configuration", "Finite gain and offset values are required."),
                [FlowCompilationDiagnosticCode.InvalidTimerDuration] = D(1307, "Invalid timer duration", "Timer duration must be from {0} through {1} milliseconds."),
                [FlowCompilationDiagnosticCode.InvalidClampConfiguration] = D(1308, "Invalid clamp configuration", "Finite minimum and maximum values are required."),
                [FlowCompilationDiagnosticCode.InvalidClampRange] = D(1309, "Invalid clamp range", "Minimum must not exceed maximum."),
                [FlowCompilationDiagnosticCode.InvalidEnabledConfiguration] = D(1310, "Invalid enabled setting", "An enabled Boolean is required."),
                [FlowCompilationDiagnosticCode.UnexpectedNodeConfiguration] = D(1311, "Unexpected node configuration", "This node requires empty configuration."),
                [FlowCompilationDiagnosticCode.InvalidFiniteNumber] = D(1312, "Invalid numeric configuration", "A finite number is required."),
                [FlowCompilationDiagnosticCode.InvalidCalculatorFormula] = D(1313, "Invalid calculator formula", "Formula must use only a, b, c, parentheses, and BODMAS operators (+, -, *, /, ^): {0}"),

                // Units
                [FlowCompilationDiagnosticCode.AnalogOutputUnitMismatch] = D(1401, "Analog output unit mismatch", "Analog output units do not match the bound point."),
                [FlowCompilationDiagnosticCode.NumericOperandUnitMismatch] = D(1403, "Numeric operand unit mismatch", "Numeric operands require identical units."),

                // Target
                [FlowCompilationDiagnosticCode.ControllerTemplateNotFound] = D(2001, "Controller template not found", "Controller template \"{0}\" was not found."),
                [FlowCompilationDiagnosticCode.ControllerTemplateIdMismatch] = D(2002, "Controller template ID mismatch", "Resolved controller template ID \"{0}\" does not match source ID \"{1}\"."),
                [FlowCompilationDiagnosticCode.ControllerTemplateRevisionMismatch] = D(2003, "Controller template revision mismatch", "Expected controller template revision {0}, but resolved revision {1}."),
                [FlowCompilationDiagnosticCode.MissingPoint] = D(2004, "Point not found", "Point \"{0}\" was not found."),
                [FlowCompilationDiagnosticCode.UnsupportedTargetConnectorCapability] = D(2005, "Unsupported target connector capability", "The target must support Boolean connectors and digital points."),
                [FlowCompilationDiagnosticCode.UnsupportedTargetFunction] = D(2006, "Unsupported target function", "The target does not support flow function \"{0}\"."),
                [FlowCompilationDiagnosticCode.TargetNodeLimitExceeded] = D(2007, "Target node limit exceeded", "The target permits at most {0} nodes per flow."),
                [FlowCompilationDiagnosticCode.TargetConnectionLimitExceeded] = D(2008, "Target connection limit exceeded", "The target permits at most {0} connections per flow."),
                [FlowCompilationDiagnosticCode.PointDirectionMismatch] = D(2009, "Incompatible point", "Point \"{0}\" is not a compatible enabled {1} {2}."),

                // Artifact
                [FlowCompilationDiagnosticCode.MalformedArtifact] = D(3001, "Malformed Flow IL artifact", "The artifact is not a valid bounded Flow IL envelope."),
                [FlowCompilationDiagnosticCode.UnsupportedFlowIlVersion] = D(3002, "Unsupported Flow IL version", "Only Flow IL version {0} can be decompiled."),
                [FlowCompilationDiagnosticCode.InvalidSectionIdentity] = D(3003, "Invalid section identity", "Sections must use canonical IDs, order, and version."),
                [FlowCompilationDiagnosticCode.InvalidSectionBounds] = D(3004, "Invalid section bounds", "Section bounds are invalid."),
                [FlowCompilationDiagnosticCode.InvalidSectionDigest] = D(3005, "Invalid section digest", "Section digest does not match its contents."),
                [FlowCompilationDiagnosticCode.TruncatedSection] = D(3006, "Truncated section", "The section record is truncated."),
                [FlowCompilationDiagnosticCode.SectionHasTrailingData] = D(3007, "Section has trailing data", "The section contains trailing bytes."),

                // Tables/records
                [FlowCompilationDiagnosticCode.InvalidConstantValue] = D(3101, "Invalid constant value", "Numeric constants must be finite."),
                [FlowCompilationDiagnosticCode.UnsupportedConstantEncoding] = D(3102, "Unsupported constant encoding", "Constant encoding is unsupported."),
                [FlowCompilationDiagnosticCode.InvalidPointEncoding] = D(3103, "Invalid point encoding", "Point binding encoding is unsupported."),
                [FlowCompilationDiagnosticCode.InvalidSlot] = D(3104, "Invalid slot", "Slot encoding is unsupported or duplicated."),
                [FlowCompilationDiagnosticCode.InvalidDependencySet] = D(3105, "Invalid dependency set", "Exactly one controller-template dependency is required."),
                [FlowCompilationDiagnosticCode.InvalidDependencyRevision] = D(3106, "Invalid dependency revision", "Dependency revision must be positive."),
                [FlowCompilationDiagnosticCode.InvalidFlowId] = D(3107, "Invalid Flow ID", "Flow ID length is invalid."),
                [FlowCompilationDiagnosticCode.EmptyEncodedIdentifier] = D(3108, "Empty encoded identifier", "Identifier must not be empty."),
                [FlowCompilationDiagnosticCode.NonCanonicalUtf8Identifier] = D(3109, "Non-canonical UTF-8 identifier", "Identifier is not canonical UTF-8."),
                [FlowCompilationDiagnosticCode.InvalidEncodedAuthoringMetadata] = D(3110, "Invalid encoded authoring metadata", "Authoring coordinates must be finite."),

                // Instructions/operands
                [FlowCompilationDiagnosticCode.InvalidFinalCommit] = D(3201, "Invalid final commit", "Commit must be the final anonymous instruction."),
                [FlowCompilationDiagnosticCode.UnsupportedOpcode] = D(3202, "Unsupported opcode", "Opcode {0} cannot be represented by the designer."),
                [FlowCompilationDiagnosticCode.InvalidResultSlot] = D(3203, "Invalid result slot", "A node result must write one unique slot."),
                [FlowCompilationDiagnosticCode.InvalidInstructionEncoding] = D(3204, "Invalid instruction encoding", "Instruction flags and reserved fields must be zero."),
                [FlowCompilationDiagnosticCode.InvalidPointOperand] = D(3205, "Invalid point operand", "Point binding is missing or has the wrong direction."),
                [FlowCompilationDiagnosticCode.UnsupportedBindingKind] = D(3206, "Unsupported binding kind", "Point binding kind is unsupported."),
                [FlowCompilationDiagnosticCode.InvalidBooleanConstantOperand] = D(3207, "Invalid Boolean constant operand", "Boolean constant index is out of range or has the wrong type."),
                [FlowCompilationDiagnosticCode.InvalidStateSlotOperand] = D(3208, "Invalid state-slot operand", "State slot is invalid."),
                [FlowCompilationDiagnosticCode.InvalidNumericConstantOperand] = D(3209, "Invalid numeric constant operand", "Numeric constant index is out of range or has the wrong type."),
                [FlowCompilationDiagnosticCode.InvalidComparisonOperand] = D(3210, "Invalid comparison operand", "Comparison operator is invalid."),
                [FlowCompilationDiagnosticCode.InvalidLevelShifterOperands] = D(3211, "Invalid level-shifter operands", "Level-shifter constants are invalid."),
                [FlowCompilationDiagnosticCode.InvalidTimerOperands] = D(3212, "Invalid timer operands", "Timer state is invalid."),
                [FlowCompilationDiagnosticCode.InvalidInputOperand] = D(3213, "Invalid input operand", "An input does not reference an earlier node result."),

                // Symbols
                [FlowCompilationDiagnosticCode.SymbolCountMismatch] = D(3301, "Symbol count mismatch", "Every instruction requires one symbol record."),
                [FlowCompilationDiagnosticCode.InvalidSymbolIndex] = D(3302, "Invalid symbol index", "Symbol indices must be canonical."),
                [FlowCompilationDiagnosticCode.UnrepresentableSymbol] = D(3303, "Unrepresentable symbol", "Instruction symbol cannot be represented as a designer node."),

                // VM
                [FlowCompilationDiagnosticCode.VmPrepareFailed] = D(4001, "VM preparation failed", "The Flow VM could not prepare the compiled artifact.")
            });

    public static FlowCompilationDiagnosticDefinition Get(FlowCompilationDiagnosticCode code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown Flow compilation diagnostic code.");

    public static string GetDisplayCode(FlowCompilationDiagnosticCode code) => Get(code).DisplayCode;

    public static string GetTitle(
        FlowCompilationDiagnosticCode code,
        Func<string, string?>? resourceLookup = null)
    {
        var definition = Get(code);
        return LocalizedOrFallback(resourceLookup, definition.TitleResourceKey, definition.Title);
    }

    public static string FormatMessage(
        FlowCompilationDiagnosticCode code,
        Func<string, string?>? resourceLookup = null,
        params object?[] arguments)
    {
        var definition = Get(code);
        var format = LocalizedOrFallback(resourceLookup, definition.MessageResourceKey, definition.MessageFormat);
        return string.Format(CultureInfo.CurrentCulture, format, arguments);
    }

    public static FlowCompilationDiagnostic Create(
        FlowCompilationDiagnosticCode code,
        string path,
        params object?[] arguments) =>
        Create(code, path, resourceLookup: null, arguments);

    public static FlowCompilationDiagnostic Create(
        FlowCompilationDiagnosticCode code,
        string path,
        Func<string, string?>? resourceLookup,
        params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var definition = Get(code);

        return new FlowCompilationDiagnostic(
            code,
            definition.DisplayCode,
            path,
            LocalizedOrFallback(resourceLookup, definition.TitleResourceKey, definition.Title),
            FormatMessage(code, resourceLookup, arguments));
    }

    private static FlowCompilationDiagnosticDefinition D(int number, string title, string messageFormat)
    {
        var code = (FlowCompilationDiagnosticCode)number;
        var name = Enum.GetName(code)
            ?? throw new InvalidOperationException($"Diagnostic code {number} has no enum member.");

        return new FlowCompilationDiagnosticDefinition(
            code,
            $"FC{number:0000}",
            $"FlowCompilationDiagnostic.{name}.Title",
            $"FlowCompilationDiagnostic.{name}.Message",
            title,
            messageFormat);
    }

    private static string LocalizedOrFallback(
        Func<string, string?>? resourceLookup,
        string resourceKey,
        string fallback)
    {
        if (resourceLookup is null)
        {
            return fallback;
        }

        var localized = resourceLookup(resourceKey);
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}

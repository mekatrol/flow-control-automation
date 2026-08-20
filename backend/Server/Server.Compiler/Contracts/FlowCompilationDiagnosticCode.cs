namespace Server.Compiler.Contracts;

/// <summary>
/// Stable machine-readable Flow compiler diagnostic codes.
///
/// Numeric values are part of the external diagnostic contract and must not be
/// renumbered after release. The numeric ranges intentionally leave room for
/// related diagnostics to be added without disturbing existing values.
///
/// Display IDs are produced as FCxxxx by <see cref="FlowCompilationDiagnostics"/>.
/// </summary>
public enum FlowCompilationDiagnosticCode
{
    None = 0,

    // 1xxx - source/request validation
    UnsupportedArtifactVersion = 1001,
    UnsupportedSourceSchema = 1002,
    InvalidFlowRevision = 1003,
    InvalidControllerTemplateRevision = 1004,
    UnsupportedExecution = 1005,
    NodeCountOutOfRange = 1006,
    ConnectionCountLimitExceeded = 1007,
    ArtifactSizeLimitExceeded = 1008,
    InvalidIdentifier = 1009,
    InvalidAuthoringMetadata = 1010,

    // 11xx - graph validation and scheduling
    DuplicateNode = 1101,
    UnsupportedNode = 1102,
    EndpointNotFound = 1103,
    InvalidConnectionDirection = 1104,
    ConnectionTypeMismatch = 1105,
    DuplicateInputDriver = 1106,
    MissingInputDriver = 1107,
    CyclicDependency = 1108,
    CombinationalCycle = 1109,
    DuplicatePointOutputDriver = 1110,

    // 12xx - flow interface validation
    UnsupportedInterfaceSchema = 1201,
    InterfaceLimitExceeded = 1202,
    InvalidInterfaceEntry = 1203,
    UnsupportedInterfaceType = 1204,
    IncompatibleInterfaceUnits = 1205,
    InvalidInterfaceDefault = 1206,
    MissingInterfaceId = 1207,
    MissingInterfaceReference = 1208,

    // 13xx - node configuration validation
    MissingPointId = 1301,
    UnexpectedPointConfigurationProperty = 1302,
    InvalidPointUnits = 1303,
    InvalidBooleanConfiguration = 1304,
    InvalidComparisonOperator = 1305,
    InvalidGainOffsetConfiguration = 1306,
    InvalidTimerDuration = 1307,
    InvalidClampConfiguration = 1308,
    InvalidClampRange = 1309,
    InvalidEnabledConfiguration = 1310,
    UnexpectedNodeConfiguration = 1311,
    InvalidFiniteNumber = 1312,

    // 14xx - unit validation
    AnalogOutputUnitMismatch = 1401,
    FlowOutputUnitMismatch = 1402,
    NumericOperandUnitMismatch = 1403,

    // 2xxx - target resolution/capability validation
    ControllerTemplateNotFound = 2001,
    ControllerTemplateIdMismatch = 2002,
    ControllerTemplateRevisionMismatch = 2003,
    MissingPoint = 2004,
    UnsupportedTargetConnectorCapability = 2005,
    UnsupportedTargetFunction = 2006,
    TargetNodeLimitExceeded = 2007,
    TargetConnectionLimitExceeded = 2008,
    PointDirectionMismatch = 2009,

    // 3xxx - Flow IL artifact/envelope/section validation
    MalformedArtifact = 3001,
    UnsupportedFlowIlVersion = 3002,
    InvalidSectionIdentity = 3003,
    InvalidSectionBounds = 3004,
    InvalidSectionDigest = 3005,
    TruncatedSection = 3006,
    SectionHasTrailingData = 3007,

    // 31xx - decoded table/record validation
    InvalidConstantValue = 3101,
    UnsupportedConstantEncoding = 3102,
    InvalidPointEncoding = 3103,
    InvalidSlot = 3104,
    InvalidDependencySet = 3105,
    InvalidDependencyRevision = 3106,
    InvalidFlowId = 3107,
    EmptyEncodedIdentifier = 3108,
    NonCanonicalUtf8Identifier = 3109,
    InvalidEncodedAuthoringMetadata = 3110,

    // 32xx - decoded instruction/operand validation
    InvalidFinalCommit = 3201,
    UnsupportedOpcode = 3202,
    InvalidResultSlot = 3203,
    InvalidInstructionEncoding = 3204,
    InvalidPointOperand = 3205,
    UnsupportedBindingKind = 3206,
    InvalidBooleanConstantOperand = 3207,
    InvalidStateSlotOperand = 3208,
    InvalidNumericConstantOperand = 3209,
    InvalidComparisonOperand = 3210,
    InvalidLevelShifterOperands = 3211,
    InvalidTimerOperands = 3212,
    InvalidInputOperand = 3213,

    // 33xx - symbol/debug reconstruction validation
    SymbolCountMismatch = 3301,
    InvalidSymbolIndex = 3302,
    UnrepresentableSymbol = 3303,

    // 4xxx - VM/runtime preparation
    VmPrepareFailed = 4001
}
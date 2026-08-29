namespace Server.Common.Contracts;

public enum FlowFunctionKind : byte
{
    And,
    Average,
    Calculator,
    Calendar,
    Clamp,
    Comparator,
    Delay,
    DigitalSwitch,
    LevelShifter,
    Line,
    Max,
    Min,
    Nand,
    Nor,
    Not,
    Or,
    Override,
    PointChanged,
    Pulse,
    ReadPoint,
    ReleasePointCommand,
    Schedule,
    AnalogSwitch,
    Sequence,
    Split,
    Timer,
    WritePoint,
    Xnor,
    Xor
}
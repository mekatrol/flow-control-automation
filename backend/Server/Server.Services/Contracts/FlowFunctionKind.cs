namespace Server.Services.Contracts;

public enum FlowFunctionKind : byte
{
    And,
    Average,
    Calculator,
    Calendar,
    Clamp,
    Comparator,
    Delay,
    If,
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
    Selector,
    Sequence,
    Split,
    Timer,
    WritePoint,
    Xnor,
    Xor
}
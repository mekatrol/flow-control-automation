namespace Server.Common.Contracts;

/// <summary>
/// Defines the instruction opcodes used by Flow IL v1.
/// </summary>
public enum FlowOpcode : byte
{
    Line = 0,
    PointInput = 1,
    DigitalConstant = 2,
    Not = 3,
    And = 4,
    Or = 5,
    Memory = 6,
    PointOutput = 7,
    MemoryCommit = 8,
    Nand = 9,
    Nor = 10,
    Xor = 11,
    Xnor = 12,
    NumericConstant = 13,
    Add = 14,
    Comparator = 15,
    LevelShifter = 16,
    QualityGood = 17,
    OnDelay = 18,
    RisingEdge = 19,

    Min = 20,
    Max = 21,
    Clamp = 22,
    Switch = 23,
    Passthrough = 24,
    DigitalSwitch = 25,
    Sequence = 26,
    Average = 27,
    A2DLow = 28,
    A2DHigh = 29,
    D2A = 30,
    Subtract = 31,
    Multiply = 32,
    Divide = 33,
    Power = 34,
    Negate = 35,
    Calculator = 36,
    CalculatorInputs = 37,

    Commit = byte.MaxValue
}

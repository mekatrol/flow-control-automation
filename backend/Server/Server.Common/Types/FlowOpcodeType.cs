namespace Server.Common.Types;

/// <summary>
/// Defines the instruction opcodes used by Flow IL v1.
/// </summary>
public enum FlowOpcodeType : byte
{
    /// <summary>
    /// Applies a configured linear transformation to a numeric value.
    /// </summary>
    Line = 0,

    /// <summary>
    /// Reads a value through a compiled point binding.
    /// </summary>
    PointInput = 1,

    /// <summary>
    /// Produces a configured Boolean constant.
    /// </summary>
    DigitalConstant = 2,

    /// <summary>
    /// Inverts a Boolean input.
    /// </summary>
    Not = 3,

    /// <summary>
    /// Produces true when all Boolean inputs are true.
    /// </summary>
    And = 4,

    /// <summary>
    /// Produces true when any Boolean input is true.
    /// </summary>
    Or = 5,

    /// <summary>
    /// Reads the stored value of a memory operation.
    /// </summary>
    Memory = 6,

    /// <summary>
    /// Writes a value through a compiled point binding.
    /// </summary>
    PointOutput = 7,

    /// <summary>
    /// Stages the next value for a memory state slot.
    /// </summary>
    MemoryCommit = 8,

    /// <summary>
    /// Produces the negation of the Boolean AND result.
    /// </summary>
    Nand = 9,

    /// <summary>
    /// Produces the negation of the Boolean OR result.
    /// </summary>
    Nor = 10,

    /// <summary>
    /// Produces the exclusive-OR result of Boolean inputs.
    /// </summary>
    Xor = 11,

    /// <summary>
    /// Produces the negation of the Boolean exclusive-OR result.
    /// </summary>
    Xnor = 12,

    /// <summary>
    /// Produces a configured numeric constant.
    /// </summary>
    AnalogConstant = 13,

    /// <summary>
    /// Adds numeric input values.
    /// </summary>
    Add = 14,

    /// <summary>
    /// Compares numeric values using the configured comparison operation.
    /// </summary>
    Comparator = 15,

    /// <summary>
    /// Applies a configured gain and offset to a numeric input.
    /// </summary>
    LevelShifter = 16,

    /// <summary>
    /// Tests whether an input value has good data quality.
    /// </summary>
    QualityGood = 17,

    /// <summary>
    /// Delays activation until the input has remained active for the configured duration.
    /// </summary>
    OnDelay = 18,

    /// <summary>
    /// Detects a transition from false to true.
    /// </summary>
    RisingEdge = 19,

    /// <summary>
    /// Selects the smallest numeric input.
    /// </summary>
    Min = 20,

    /// <summary>
    /// Selects the largest numeric input.
    /// </summary>
    Max = 21,

    /// <summary>
    /// Restricts a numeric value to configured lower and upper bounds.
    /// </summary>
    Clamp = 22,

    /// <summary>
    /// Selects a numeric value according to a control input.
    /// </summary>
    Switch = 23,

    /// <summary>
    /// Copies an input value to an output without transforming it.
    /// </summary>
    Passthrough = 24,

    /// <summary>
    /// Selects a Boolean value according to a control input.
    /// </summary>
    DigitalSwitch = 25,

    /// <summary>
    /// Advances through a configured sequence of output states.
    /// </summary>
    Sequence = 26,

    /// <summary>
    /// Calculates the arithmetic mean of numeric inputs.
    /// </summary>
    Average = 27,

    /// <summary>
    /// Applies the low threshold and previous state during analog-to-digital conversion.
    /// </summary>
    A2DLow = 28,

    /// <summary>
    /// Applies the high threshold and stages the next analog-to-digital conversion state.
    /// </summary>
    A2DHigh = 29,

    /// <summary>
    /// Converts a digital input to a configured analog value.
    /// </summary>
    D2A = 30,

    /// <summary>
    /// Subtracts one numeric operand from another.
    /// </summary>
    Subtract = 31,

    /// <summary>
    /// Multiplies numeric operands.
    /// </summary>
    Multiply = 32,

    /// <summary>
    /// Divides one numeric operand by another.
    /// </summary>
    Divide = 33,

    /// <summary>
    /// Raises a numeric operand to a specified power.
    /// </summary>
    Power = 34,

    /// <summary>
    /// Changes the sign of a numeric value.
    /// </summary>
    Negate = 35,

    /// <summary>
    /// Evaluates a configured numeric expression.
    /// </summary>
    Calculator = 36,

    /// <summary>
    /// Supplies additional input operands for a calculator instruction.
    /// </summary>
    CalculatorInputs = 37,

    /// <summary>
    /// Delays a signal according to the configured timing behavior.
    /// </summary>
    Delay = 38,

    /// <summary>
    /// Produces a pulse with a configured duration.
    /// </summary>
    Pulse = 39,

    /// <summary>
    /// Counts input events using persistent counter state.
    /// </summary>
    Counter = 40,

    /// <summary>
    /// Produces a periodic signal using configured timing.
    /// </summary>
    Clock = 41,

    /// <summary>
    /// Commits staged state changes at the end of a scan.
    /// </summary>
    Commit = byte.MaxValue
}
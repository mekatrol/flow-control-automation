using System.Text.Json.Serialization;

namespace Server.Common.Types;

/// <summary>
/// Identifies the operation or point represented by a flow node.
/// </summary>
public enum FlowNodeType : byte
{
    /// <summary>
    /// Represents a node whose operation is not recognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Reads a Boolean value from an input point.
    /// </summary>
    DigitalInput = 1,

    /// <summary>
    /// Writes a Boolean value to an output point.
    /// </summary>
    DigitalOutput,

    /// <summary>
    /// Produces a configured Boolean constant.
    /// </summary>
    DigitalConstant,

    /// <summary>
    /// Reads a numeric value from an input point.
    /// </summary>
    AnalogInput,

    /// <summary>
    /// Writes a numeric value to an output point.
    /// </summary>
    AnalogOutput,

    /// <summary>
    /// Inverts a Boolean input.
    /// </summary>
    Not,

    /// <summary>
    /// Produces true when all Boolean inputs are true.
    /// </summary>
    And,

    /// <summary>
    /// Produces the negation of the Boolean AND result.
    /// </summary>
    Nand,

    /// <summary>
    /// Produces true when any Boolean input is true.
    /// </summary>
    Or,

    /// <summary>
    /// Produces the negation of the Boolean OR result.
    /// </summary>
    Nor,

    /// <summary>
    /// Produces the exclusive-OR result of Boolean inputs.
    /// </summary>
    Xor,

    /// <summary>
    /// Produces the negation of the Boolean exclusive-OR result.
    /// </summary>
    Xnor,

    /// <summary>
    /// Reads the stored value of a memory operation.
    /// </summary>
    Memory,

    /// <summary>
    /// Tests whether an input value has good data quality.
    /// </summary>
    QualityGood,

    /// <summary>
    /// Produces a configured numeric constant.
    /// </summary>
    AnalogConstant,

    /// <summary>
    /// Adds numeric input values.
    /// </summary>
    Add,

    /// <summary>
    /// Compares numeric values using the configured comparison operation.
    /// </summary>
    Comparator,

    /// <summary>
    /// Applies a configured gain and offset to a numeric input.
    /// </summary>
    LevelShifter,

    /// <summary>
    /// Delays activation until the input has remained active for the configured duration.
    /// </summary>
    OnDelay,

    /// <summary>
    /// Detects a transition from false to true.
    /// </summary>
    RisingEdge,

    /// <summary>
    /// Calculates the arithmetic mean of numeric inputs.
    /// </summary>
    Average,

    /// <summary>
    /// Evaluates a configured numeric expression.
    /// </summary>
    Calculator,

    /// <summary>
    /// Restricts a numeric value to configured lower and upper bounds.
    /// </summary>
    Clamp,

    /// <summary>
    /// Selects the smallest numeric input.
    /// </summary>
    Min,

    /// <summary>
    /// Selects the largest numeric input.
    /// </summary>
    Max,

    /// <summary>
    /// Applies a configured linear transformation to a numeric value.
    /// </summary>
    Line,

    /// <summary>
    /// Selects a Boolean value according to a control input.
    /// </summary>
    DigitalSwitch,

    /// <summary>
    /// Selects a numeric value according to a control input.
    /// </summary>
    AnalogSwitch,

    /// <summary>
    /// Distributes an input value to multiple outputs.
    /// </summary>
    Split,

    /// <summary>
    /// Advances through a configured sequence of output states.
    /// </summary>
    Sequence,

    /// <summary>
    /// Applies an override value according to the configured control.
    /// </summary>
    Override,

    /// <summary>
    /// Delays a signal according to the configured timing behavior.
    /// </summary>
    Delay,

    /// <summary>
    /// Provides a configured time-dependent flow function.
    /// </summary>
    Timer,

    /// <summary>
    /// Produces a pulse with a configured duration.
    /// </summary>
    Pulse,

    /// <summary>
    /// Controls a value according to a configured time schedule.
    /// </summary>
    Schedule,

    /// <summary>
    /// Controls a value according to configured calendar conditions.
    /// </summary>
    Calendar,

    /// <summary>
    /// Converts an analog value to a Boolean output using low and high thresholds.
    /// </summary>
    [JsonStringEnumMemberName("a2d")]
    A2D,

    /// <summary>
    /// Converts a digital input to a configured analog value.
    /// </summary>
    [JsonStringEnumMemberName("d2a")]
    D2A,

    /// <summary>
    /// Subtracts one numeric operand from another.
    /// </summary>
    Subtract,

    /// <summary>
    /// Multiplies numeric operands.
    /// </summary>
    Multiply,

    /// <summary>
    /// Divides one numeric operand by another.
    /// </summary>
    Divide,

    /// <summary>
    /// Raises a numeric operand to a specified power.
    /// </summary>
    Power,

    /// <summary>
    /// Changes the sign of a numeric value.
    /// </summary>
    Negate,

    /// <summary>
    /// Counts input events using persistent counter state.
    /// </summary>
    Counter,

    /// <summary>
    /// Produces a periodic signal using configured timing.
    /// </summary>
    Clock,

    /// <summary>
    /// Represents an internal numeric point with no direct physical I/O mapping.
    /// </summary>
    AnalogVirtual,

    /// <summary>
    /// Represents an internal Boolean point with no direct physical I/O mapping.
    /// </summary>
    DigitalVirtual
}
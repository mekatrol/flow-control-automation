using System.Text.Json.Serialization;

namespace Server.Common.Types;

/// <summary>
/// Identifies a flow function advertised by a controller template.
/// </summary>
public enum FlowFunctionType : byte
{
    /// <summary>
    /// Produces true when all Boolean inputs are true.
    /// </summary>
    And,

    /// <summary>
    /// Calculates the arithmetic mean of numeric inputs.
    /// </summary>
    Average,

    /// <summary>
    /// Evaluates a configured numeric expression.
    /// </summary>
    Calculator,

    /// <summary>
    /// Controls a value according to configured calendar conditions.
    /// </summary>
    Calendar,

    /// <summary>
    /// Restricts a numeric value to configured lower and upper bounds.
    /// </summary>
    Clamp,

    /// <summary>
    /// Compares numeric values using the configured comparison operation.
    /// </summary>
    Comparator,

    /// <summary>
    /// Delays a signal according to the configured timing behavior.
    /// </summary>
    Delay,

    /// <summary>
    /// Selects a Boolean value according to a control input.
    /// </summary>
    DigitalSwitch,

    /// <summary>
    /// Applies a configured gain and offset to a numeric input.
    /// </summary>
    LevelShifter,

    /// <summary>
    /// Applies a configured linear transformation to a numeric value.
    /// </summary>
    Line,

    /// <summary>
    /// Selects the largest numeric input.
    /// </summary>
    Max,

    /// <summary>
    /// Selects the smallest numeric input.
    /// </summary>
    Min,

    /// <summary>
    /// Produces the negation of the Boolean AND result.
    /// </summary>
    Nand,

    /// <summary>
    /// Produces the negation of the Boolean OR result.
    /// </summary>
    Nor,

    /// <summary>
    /// Inverts a Boolean input.
    /// </summary>
    Not,

    /// <summary>
    /// Produces true when any Boolean input is true.
    /// </summary>
    Or,

    /// <summary>
    /// Applies an override value according to the configured control.
    /// </summary>
    Override,

    /// <summary>
    /// Detects a change in a point value.
    /// </summary>
    PointChanged,

    /// <summary>
    /// Produces a pulse with a configured duration.
    /// </summary>
    Pulse,

    /// <summary>
    /// Reads the current value of a point.
    /// </summary>
    ReadPoint,

    /// <summary>
    /// Releases an active command on a point.
    /// </summary>
    ReleasePointCommand,

    /// <summary>
    /// Controls a value according to a configured time schedule.
    /// </summary>
    Schedule,

    /// <summary>
    /// Selects a numeric value according to a control input.
    /// </summary>
    AnalogSwitch,

    /// <summary>
    /// Advances through a configured sequence of output states.
    /// </summary>
    Sequence,

    /// <summary>
    /// Distributes an input value to multiple outputs.
    /// </summary>
    Split,

    /// <summary>
    /// Provides a configured time-dependent flow function.
    /// </summary>
    Timer,

    /// <summary>
    /// Issues a value command to a point.
    /// </summary>
    WritePoint,

    /// <summary>
    /// Produces the negation of the Boolean exclusive-OR result.
    /// </summary>
    Xnor,

    /// <summary>
    /// Produces the exclusive-OR result of Boolean inputs.
    /// </summary>
    Xor,

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
    Negate
}
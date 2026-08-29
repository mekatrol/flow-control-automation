namespace Server.Common.Contracts;

public enum FlowSlotKind : byte
{
    Value = 2,
    MemoryState = 3,
    TimerState = 4,
    EdgeState = 5,
    CounterState = 6
}
namespace Server.Services.Contracts;

internal enum FlowSlotKind : byte
{
    Value = 2,
    MemoryState = 3,
    TimerState = 4,
    EdgeState = 5
}
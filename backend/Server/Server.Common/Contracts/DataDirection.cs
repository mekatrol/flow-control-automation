namespace Server.Common.Contracts;

public enum DataDirection : byte
{
    Input = 1,
    Output,
    InputOutput,
    Value
}
namespace Server.Common.Contracts;

public enum FlowPointValueType : byte
{
    Analog = 1,
    Digital,
    MultiState,
    Integer,
    Text
}

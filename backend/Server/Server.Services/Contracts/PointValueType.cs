namespace Server.Services.Contracts;

public enum PointValueType : byte
{
    Analog = 1,
    Digital,
    MultiState,
    Integer,
    Text
}

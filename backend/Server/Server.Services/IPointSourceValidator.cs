using Server.Services.Contracts;

namespace Server.Services;

public interface IPointSourceValidator
{
    void Validate(PointSource source);
}
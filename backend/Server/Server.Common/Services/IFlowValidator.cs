using Server.Common.Contracts;

namespace Server.Common.Services;

public interface IFlowValidator
{
    void Validate(Flow flow);
}
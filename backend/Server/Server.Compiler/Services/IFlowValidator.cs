using Server.Common.Contracts;

namespace Server.Compiler.Services;

public interface IFlowValidator
{
    void Validate(Flow flow);
}
using Server.Common.Models;

namespace Server.Compiler.Services;

public interface IFlowValidator
{
    void Validate(Flow flow);
}
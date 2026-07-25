using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowRuntimeService
{
    RuntimeSnapshot Get(Flow flow);

    RuntimeSnapshot Deploy(Flow flow);

    RuntimeSnapshot Stop(Flow flow);

    void Delete(string flowId);
}
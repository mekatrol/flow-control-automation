namespace Server.Services;

/// <summary>
/// The persistence boundary for validated flows. It intentionally exposes domain
/// contracts rather than EF entities so callers cannot depend on the schema.
/// </summary>
public interface IFlowStore : IFlowService;
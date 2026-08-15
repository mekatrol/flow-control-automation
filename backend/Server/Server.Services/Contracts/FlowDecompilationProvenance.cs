namespace Server.Services.Contracts;

public sealed record FlowDecompilationProvenance(
    int ArtifactVersion,
    string ArtifactSha256,
    uint FlowRevision,
    string ControllerTemplateId,
    uint ControllerTemplateRevision);
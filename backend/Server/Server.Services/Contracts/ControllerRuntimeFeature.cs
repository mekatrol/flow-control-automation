namespace Server.Services.Contracts;

public enum ControllerRuntimeFeature : byte
{
    VirtualPoints,
    BoundPoints,
    CommandArbitration,
    QualityPropagation
}
namespace Server.Common.Contracts.Points;

public interface IPointMappingResolver
{
    PointMappingResolution Resolve(PointSource source, string pointId);
    PointMappingResolution Resolve(PointSource source, AutomationPoint point);
}
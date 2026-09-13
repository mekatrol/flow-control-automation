namespace Server.Common.Contracts.Points;

public interface IPointValueConverter
{
    PointValueConversionResult Parse(AutomationPoint point, string? rawValue);
    string Serialize(AutomationPoint point, object? value);
}
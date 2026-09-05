using Server.Common.Models;
using Server.Common.Types;

namespace Server.Compiler.Services.Implementation;

public static class ControllerCapabilitiesSupport
{
    public static bool SupportsPoint(
        ValidatedControllerTemplate template,
        AutomationPointValueType valueType,
        DataDirectionType direction) =>
        template.PointTypes.Contains(valueType)
        && template.PointDirections.Contains(direction);

    public static bool SupportsPointFeature(
        ValidatedControllerTemplate template,
        ControllerPointFeatureType feature) =>
        template.PointFeatures.Contains(feature);

    public static bool SupportsConnector(
        ValidatedControllerTemplate template,
        ConnectorDataType dataType) =>
        template.ConnectorDataTypes.Contains(dataType);

    public static bool SupportsFunction(
        ValidatedControllerTemplate template,
        FlowFunctionType function) =>
        template.FlowFunctions.Contains(function);

    public static bool SupportsExecutionMode(
        ValidatedControllerTemplate template,
        ExecutionModeType mode) =>
        template.ExecutionModes.Contains(mode);

    public static bool SupportsRuntimeFeature(
        ValidatedControllerTemplate template,
        ControllerRuntimeFeatureType feature) =>
        template.RuntimeFeatures.Contains(feature);
}
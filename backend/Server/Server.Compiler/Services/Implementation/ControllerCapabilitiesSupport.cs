using Server.Common.Contracts;
using Server.Common.Models;

namespace Server.Compiler.Services.Implementation;

public static class ControllerCapabilitiesSupport
{
    public static bool SupportsPoint(
        ValidatedControllerTemplate template,
        FlowPointValueType valueType,
        DataDirection direction) =>
        template.PointTypes.Contains(valueType)
        && template.PointDirections.Contains(direction);

    public static bool SupportsPointFeature(
        ValidatedControllerTemplate template,
        ControllerPointFeature feature) =>
        template.PointFeatures.Contains(feature);

    public static bool SupportsConnector(
        ValidatedControllerTemplate template,
        ConnectorDataType dataType) =>
        template.ConnectorDataTypes.Contains(dataType);

    public static bool SupportsFunction(
        ValidatedControllerTemplate template,
        FlowFunctionKind function) =>
        template.FlowFunctions.Contains(function);

    public static bool SupportsExecutionMode(
        ValidatedControllerTemplate template,
        ExecutionMode mode) =>
        template.ExecutionModes.Contains(mode);

    public static bool SupportsRuntimeFeature(
        ValidatedControllerTemplate template,
        ControllerRuntimeFeature feature) =>
        template.RuntimeFeatures.Contains(feature);
}
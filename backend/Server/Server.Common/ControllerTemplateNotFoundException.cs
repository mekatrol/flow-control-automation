namespace Server.Common;

public sealed class ControllerTemplateNotFoundException(string id)
    : Exception($"Controller template \"{id}\" was not found.");
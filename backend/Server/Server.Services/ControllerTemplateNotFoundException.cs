namespace Server.Services;

public sealed class ControllerTemplateNotFoundException(string id)
    : Exception($"Controller template \"{id}\" was not found.");
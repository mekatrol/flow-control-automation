namespace Server.Services;

public sealed class PointDefinitionNotFoundException(string resource, string id)
    : Exception($"{resource} \"{id}\" was not found.");
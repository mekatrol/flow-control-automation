namespace Server.Services;

public sealed class PointSourceNotFoundException(string id)
    : Exception($"Point source {id} was not found.");
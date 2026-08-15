namespace Server.Services;

public sealed class FlowEmulatorNotFoundException(string emulatorId)
    : Exception($"Emulator '{emulatorId}' was not found.");
namespace Server.Services;

public enum FlowVirtualMachineErrorCode : byte
{
    InvalidImage = 1,
    UnsupportedVersion = 2,
    InvalidEnvelope = 3,
    InvalidSection = 4,
    InvalidIdentifier = 5,
    InvalidConstant = 6,
    InvalidStateLayout = 7,
    InvalidOpcode = 8,
    InvalidInstruction = 9,
    InvalidLifecycleState = 10,
    InvalidRuntimeInput = 11
}

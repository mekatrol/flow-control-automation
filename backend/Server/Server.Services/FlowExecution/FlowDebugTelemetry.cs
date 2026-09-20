using System.Diagnostics.Metrics;

namespace Server.Services.FlowExecution;

internal static class FlowDebugTelemetry
{
    private const string MeterName = "FlowControl.FlowDebug";

    private static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> _leaseAcquisitionConflicts = Meter.CreateCounter<long>(
        "flow_debug.lease_acquisition_conflicts");
    internal static readonly Counter<long> _suspensionFailures = Meter.CreateCounter<long>(
        "flow_debug.suspension_failures");
    internal static readonly Counter<long> _resumeFailures = Meter.CreateCounter<long>(
        "flow_debug.resume_failures");
    internal static readonly Counter<long> _forcedStops = Meter.CreateCounter<long>(
        "flow_debug.forced_stops");
    internal static readonly Counter<long> _executionTimestampCheckpointFailures = Meter.CreateCounter<long>(
        "flow_debug.execution_timestamp_checkpoint_failures");

}
namespace Server.Services.Points.Runtime;

#pragma warning disable CC0003, SA1402, SA1649

internal abstract class UnavailablePointMappingAdapter(TimeProvider timeProvider) : IPointMappingAdapter
{
    protected abstract string Label { get; }
    public abstract PointSourceKind Kind { get; }

    public Task<PointMappingReadResult> ReadAsync(PointMappingResolution resolution, CancellationToken cancellationToken) =>
        Task.FromResult(new PointMappingReadResult(new Dictionary<string, string?>(), DataQualityType.Unavailable,
            timeProvider.GetUtcNow(), $"{Label} mapping has not produced a commissioned sample."));

    public Task<PointMappingCommandResult> CommandAsync(PointMappingResolution resolution, object? value, CancellationToken cancellationToken) =>
        Task.FromResult(new PointMappingCommandResult(string.Empty, timeProvider.GetUtcNow(),
            $"{Label} mapping has no commissioned command transport."));
}

internal sealed class MqttPointMappingAdapter(TimeProvider timeProvider) : UnavailablePointMappingAdapter(timeProvider)
{ protected override string Label => "MQTT"; public override PointSourceKind Kind => PointSourceKind.Mqtt; }
internal sealed class HomeAssistantPointMappingAdapter(TimeProvider timeProvider) : UnavailablePointMappingAdapter(timeProvider)
{ protected override string Label => "Home Assistant"; public override PointSourceKind Kind => PointSourceKind.HomeAssistant; }
internal sealed class PhysicalPointMappingAdapter(TimeProvider timeProvider) : UnavailablePointMappingAdapter(timeProvider)
{ protected override string Label => "Physical"; public override PointSourceKind Kind => PointSourceKind.Physical; }
internal sealed class VirtualPointMappingAdapter(TimeProvider timeProvider) : UnavailablePointMappingAdapter(timeProvider)
{ protected override string Label => "Virtual"; public override PointSourceKind Kind => PointSourceKind.Virtual; }
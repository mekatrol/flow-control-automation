using Server.Common.Contracts;
using Server.Common.Types;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.Common.Models;

/// <summary>
/// Describes a named value that automation flows can read, command, or both.
/// </summary>
/// <remarks>
/// A point is the configuration contract between a flow and a value. Derived records identify
/// whether that value lives in memory, on local hardware, or behind a remote integration. The
/// remaining members describe its data shape, capabilities, binding, and safe runtime behavior.
/// </remarks>
[JsonConverter(typeof(AutomationPointJsonConverter))]
public abstract record AutomationPoint : IAutomationPoint
{
    /// <summary>
    /// Gets the stable, machine-readable identifier used to reference the point from configuration
    /// and flow definitions.
    /// </summary>
    /// <remarks>
    /// This is separate from <see cref="Name"/> so the display name can change without breaking
    /// references. Valid identifiers are normalized lowercase, hyphen-separated values.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the human-readable name shown to operators and configuration authors.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets optional operator-facing context explaining what the point represents or controls.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets whether the point may participate in runtime operations.
    /// </summary>
    /// <remarks>
    /// Keeping disabled points in the catalogue preserves references and configuration while
    /// preventing the runtime from treating them as available.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the optional point-group identifier used to organise the point and inherit
    /// group-level remote-source configuration.
    /// </summary>
    /// <remarks>
    /// A null value makes the point standalone. For remote points, the referenced group may supply
    /// <see cref="SourceId"/> and mapping defaults; local points may only join local groups.
    /// </remarks>
    public string? GroupId { get; set; }

    /// <summary>
    /// Gets how data is allowed to move through the point: into a flow, out to a target, in both
    /// directions, or as a directionless virtual value.
    /// </summary>
    /// <remarks>
    /// Direction constrains <see cref="Readable"/> and <see cref="Commandable"/> so invalid
    /// combinations cannot be deployed.
    /// </remarks>
    public required DataDirectionType Direction { get; init; }

    /// <summary>
    /// Gets the logical data type used to validate, display, and exchange the point's values.
    /// </summary>
    public required AutomationPointValueType ValueType { get; init; }

    /// <summary>
    /// Gets where the point's value originates or is applied.
    /// </summary>
    /// <remarks>
    /// The discriminator is supplied by the derived record and is used during polymorphic JSON
    /// serialization and source-specific validation.
    /// </remarks>
    public abstract PointSourceType PointSourceType { get; }

    /// <summary>
    /// Gets the optional normalized engineering-unit identifier for numeric values, such as
    /// <c>degC</c> or <c>percent</c>.
    /// </summary>
    /// <remarks>Units are valid only for analog and integer points.</remarks>
    public string? Units { get; init; }

    /// <summary>
    /// Gets the labels that translate machine states into operator-facing text.
    /// </summary>
    /// <remarks>
    /// Digital points require an object containing distinct <c>false</c> and <c>true</c> labels.
    /// Multi-state points require an array of unique <c>key</c>/<c>label</c> objects. Other value
    /// types do not accept state labels.
    /// </remarks>
    public JsonNode? StateLabels { get; init; }

    /// <summary>
    /// Gets whether consumers are allowed to obtain the point's current value.
    /// </summary>
    public bool Readable { get; init; }

    /// <summary>
    /// Gets whether flows or operators are allowed to write commands to the point.
    /// </summary>
    /// <remarks>
    /// Commandable non-virtual points must define <see cref="SafeDisablePolicy"/> so loss-of-control
    /// transitions have explicit behavior.
    /// </remarks>
    public bool Commandable { get; init; }

    /// <summary>
    /// Gets whether the runtime value is <c>volatile</c> or <c>retained</c> across runtime restarts.
    /// </summary>
    /// <remarks>
    /// Persistence is represented as a string because it is part of the external JSON/YAML
    /// contract. Retention is meaningful for virtual point storage; a retained virtual point also
    /// requires <see cref="RelinquishDefault"/> as a deterministic initial fallback.
    /// </remarks>
    public required string Persistence { get; init; }

    /// <summary>
    /// Gets the type-appropriate fallback value exposed when no writer currently supplies a value
    /// for a virtual point.
    /// </summary>
    /// <remarks>
    /// “Relinquish” means that a writer has released ownership of the value. The runtime then uses
    /// this default and reports good quality; without it, an unwritten point is unavailable. The
    /// JSON value must match <see cref="ValueType"/>, configured <see cref="Limits"/>, and any
    /// multi-state keys. It is required for retained virtual points so they have a valid value when
    /// no retained value has yet been stored.
    /// </remarks>
    public JsonNode? RelinquishDefault { get; init; }

    /// <summary>
    /// Gets the point-source identifier used by a remote point to locate its protocol connection.
    /// </summary>
    /// <remarks>
    /// A remote point may specify this directly or inherit it from <see cref="GroupId"/>. Virtual
    /// and physical points must leave it null. Credentials belong to the referenced source, not
    /// in this point definition.
    /// </remarks>
    public string? SourceId { get; init; }

    /// <summary>
    /// Gets the protocol-specific address and read/write settings that bind a remote point to its
    /// external value.
    /// </summary>
    /// <remarks>
    /// Its shape depends on the referenced source kind—for example MQTT topics, a Home Assistant
    /// entity, or an HTTP path. Remote points require a mapping; virtual and physical points do not
    /// accept one.
    /// </remarks>
    public JsonObject? Mapping { get; init; }

    /// <summary>
    /// Gets optional bounds used to reject values that cannot safely or validly belong to the point.
    /// </summary>
    /// <remarks>
    /// Analog and integer points may define <c>minimum</c> and <c>maximum</c>. Text points must
    /// define a positive <c>maximumLength</c>. Limits also constrain
    /// <see cref="RelinquishDefault"/>.
    /// </remarks>
    public JsonObject? Limits { get; init; }

    /// <summary>
    /// Gets the required action for a commandable non-virtual point during startup, shutdown,
    /// communication loss, and explicit disable transitions.
    /// </summary>
    /// <remarks>
    /// Making these policies explicit prevents an actuator's behavior from being left to an
    /// integration-specific default. Each transition selects <c>hold_last</c>, <c>safe_value</c>,
    /// <c>relinquish</c>, or <c>stop_driving</c>. Non-commandable points do not accept a policy.
    /// </remarks>
    public JsonObject? SafeDisablePolicy { get; init; }

    /// <summary>
    /// Gets the persisted version used for optimistic concurrency when updating or deleting the
    /// point.
    /// </summary>
    /// <remarks>A zero value is omitted from serialized transport documents.</remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Revision { get; init; }

    /// <summary>
    /// Gets the server-generated creation timestamp, or null for definitions that have not been
    /// persisted or are being exported as portable configuration.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedAt { get; init; }

    /// <summary>
    /// Gets the server-generated timestamp of the most recent persisted update, or null when that
    /// metadata is unavailable or intentionally excluded from transport.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedAt { get; init; }
}
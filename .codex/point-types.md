# Point model

## Intent

The flow controller needs a first-class concept of a **point** in both the
frontend and backend. A point is the stable boundary between control logic and
the value being monitored or controlled. 

Flows read points, calculate results,
and write points without needing to know whether the value originated in local
memory, an I/O terminal, another controller, MQTT, or an HTTP API.

The point model places physical inputs and outputs, external-system values, and
internal values behind one consistent interface. Internal constants, logic
results, accumulators, setpoints, schedules, and other calculated values can
therefore participate in flows in the same way as field data, while still
retaining metadata about their origin and capabilities.

A point is more than a variable. It has:

- a stable identity and human-readable metadata;
- a typed present value and engineering units;
- a source or destination binding;
- value quality, timestamp, and communication status;
- service, override, and command state;
- optional limits, alarming, history, and fallback behaviour; and
- permissions governing who or what may command it.

These concerns must be represented independently. For example, a physical
temperature input can be enabled, reliable, in alarm, and manually simulated at
the same time. A single `state` enum cannot describe all of those facts.

## Terminology

The following terms have precise meanings:

- **Point definition**: persistent configuration and metadata.
- **Point binding**: how a point exchanges data with memory, hardware, or an
  external system.
- **Present value**: the effective value currently exposed to flows and users.
- **Raw value**: the unscaled value received from a binding, where applicable.
- **Command**: a request from a flow, schedule, operator, safety function, or
  integration to set a writable point.
- **Effective command**: the active command selected by command arbitration.
- **Relinquish**: remove a command so the next applicable command, or the
  configured default, becomes effective.
- **Override**: a command deliberately given precedence over normal automatic
  logic. An override is not the same thing as disabling a point.
- **Out of service**: disconnect normal live updates from the binding so a value
  can be supplied for testing or commissioning.
- **Quality/reliability**: whether the present value can be trusted.

The term **point** describes the complete object. The value is only one
attribute of that object and should not be passed around without its type,
quality, and timestamp when those details affect control decisions.

## Independent classification axes

“Point type” is often used to mean several different things. The data model
should use separate fields for implementation, direction, value type, and
electrical behaviour.

### Implementation

#### Virtual

A virtual point is held by the flow controller and has no direct physical or
external binding. Examples include:

- occupied cooling setpoint;
- calculated dew point;
- plant enable request;
- last mode selected by an operator;
- loop output before it is mapped to hardware;
- counter, timer, accumulator, or latched alarm;
- constant or tunable parameter.

Virtual points should be durable by default when they represent configuration
or operator intent. Intermediate calculated values may instead be volatile and
reset when the engine restarts. Persistence is therefore a separate
configuration field, not implied by being virtual.

#### Bound

A bound point connects to something outside the flow engine. The binding may
represent:

- local controller I/O;
- a remote PLC, DDC, or building controller;
- BACnet, Modbus, KNX, or another field protocol;
- an MQTT topic;
- a Home Assistant entity;
- an HTTP or vendor API;
- a database, message bus, or another flow-controller instance.

“Physical” alone is too narrow because an MQTT value or API property is external
but may not correspond to a wire. The UI may display friendlier subtypes such as
**Physical I/O** and **External system**, while the engine treats both as bound
points with different drivers.

Each binding should define its address, read/write capability, polling or
subscription behaviour, timeout, conversion, and reconnect policy. Secrets must
be referenced through a credential store rather than embedded in the point.

### Direction and capability

Direction describes the relationship between the point and its binding, not the
direction of every connection drawn in a flow:

- **Input**: the binding supplies the value; normal flow logic reads it.
- **Output**: commands are sent through the binding; normal flow logic writes it.
- **Input/output**: the binding supports both reading and commanding.
- **Value**: an internal value has no inherent field direction and may be read
  and written according to its permissions.

Readability and commandability should also be explicit capabilities. A point can
be readable but not commandable, commandable with readback, or write-only.

A virtual setpoint illustrates why flow-graph direction is separate from point
direction. The same setpoint may be read by a temperature-control flow and
written by a reset flow that adjusts it for occupancy, time of day, or outside
air temperature. The point remains one shared value, not two unrelated points.

### Value type

The initial supported value types are:

- **Analog**: a finite floating-point value, normally with engineering units,
  precision, and optional minimum and maximum.
- **Digital**: a two-state value. State labels are configurable, for example
  `Off/On`, `Closed/Open`, `Normal/Alarm`, or `Stop/Run`; the stored value
  remains boolean.
- **Multi-state**: one value from a configured enumeration, for example
  `Off`, `Heat`, `Cool`, `Auto`. Tri-state is a three-member multi-state point
  when all three choices are logical states.
- **Integer**: a whole-number value for counters, indexes, and quantities where
  floating point is inappropriate.
- **Text**: a bounded string for status or integration data. It should not be a
  default transport for values that can be strongly typed.

Future types may include date/time, duration, structured data, or binary data,
but flows should not silently coerce incompatible point types.

For analog points, units are part of the contract. The engine should reject or
explicitly convert connections such as degrees Celsius to degrees Fahrenheit
and should reject nonsensical connections such as temperature to pressure.
Display precision must not reduce the precision used by control logic.

### Electrical output mode

Open collector describes electrical drive behaviour, not a third logical value
type. It therefore belongs in the hardware binding:

- **Driven digital**: actively drives both inactive and active states.
- **Open collector/open drain**: asserts by pulling low and relinquishes by
  becoming high impedance.
- **Tri-state output**: can drive low, drive high, or become high impedance.
- **Analog voltage/current**: for example 0–10 V or 4–20 mA.
- **Position-adjust/floating actuator**: paired increase/decrease outputs with
  travel timing and interlocking.

For an open-collector output, the logical value is normally `asserted` or
`released`; `released` means electrically floating and is not evidence that the
external circuit is high. If feedback matters, it must be represented by a
separate input or readback value. Driver inversion can map `true` to pull-low
without leaking active-low electrical details into every flow.

## Point data

A useful minimum point definition is:

```text
id                    stable machine identifier
name                  unique display name within its scope
description           optional operator/engineering description
enabled               whether the point participates in runtime processing
implementation        virtual | bound
direction             input | output | input_output | value
value_type            analog | digital | multi_state | integer | text
units                 engineering-unit identifier, when applicable
state_labels          labels for digital or multi-state values
readable              whether clients and flows may read it
commandable           whether authorised clients and flows may command it
persistence           volatile | retained
relinquish_default    value used when no command is active
binding               driver-specific configuration, for bound points
limits                valid range, clamp/reject policy, and rate limits
alarm                  optional alarm configuration
history                optional trend configuration
```

Runtime data is separate from the definition:

```text
present_value         effective typed value exposed by the point
source_value          latest value received from the binding
raw_value             optional unscaled driver value
effective_command     winning command and its source
quality               good | uncertain | bad
reliability_reason    specific reason when quality is not good
in_service            whether normal binding processing is active
overridden            derived: an override command is effective
in_alarm              derived from alarm evaluation
last_update_at        when any runtime property last changed
value_timestamp       when the source produced or changed the value
last_good_value       last value whose quality was good
```

For a non-commandable virtual point, `relinquish_default` is its initial value.
For a commandable point, it is the value selected after all commands have been
released, unless the binding is configured to relinquish control externally.

The API should return the typed value together with quality and timestamp.
Consumers must not have to make a second request to discover that a value is
stale or simulated.

## Lifecycle, service, and status

The original concepts of enabled, disabled, overridden, and auto are retained,
but represented without forcing them into one mutually exclusive state.

### Enabled and disabled

`enabled` is administrative configuration. When disabled:

- the driver does not poll, subscribe, or write;
- the flow engine does not propagate point changes;
- queued commands are not silently applied to hardware;
- the last value may remain visible, but its quality indicates `disabled`; and
- disabling an output invokes its configured safe-disable policy.

The safe-disable policy must be explicit: hold last value, command a safe value,
relinquish to the external controller, or stop driving the output. The system
must not assume that `false` or zero is safe for every item of plant.

### In service and out of service

Out of service is a commissioning feature, distinct from disabled. When an input
is out of service, live binding updates no longer determine its present value
and an authorised engineer may supply a simulated value. When an output is out
of service, commands can be exercised without being sent to field hardware,
unless a binding explicitly provides a separate test mode.

Returning a point to service reconnects it to the source or destination in a
controlled way. For an output, the engine should recalculate the winning command
before writing, preventing an old test value from being unexpectedly applied.

Out of service and overridden must remain separate: one disconnects normal
field behaviour, while the other changes which command wins during normal
command arbitration.

### Automatic and overridden

`auto` is not an administrative state. A point is operating automatically when
its effective value comes from a command class designated as automatic, such as
normal flow control, scheduling, or plant sequencing. `overridden` is a derived
status indicating that a manual or other override-class command is effective.
The runtime state identifies which source currently wins and when that command
expires, if applicable.

Operators should always be able to see:

- the present value;
- the source of the effective command;
- its priority;
- whether it is temporary or persistent;
- the value that automatic logic is requesting underneath it; and
- how to release it.

## Command arbitration

Multiple flows, schedules, safeties, and operators may command the same point.
Last-write-wins is unsafe and makes control behaviour difficult to diagnose.

Writable points therefore need a command table similar in spirit to the
priority-based control commonly used in automation systems.

Each command contains:

```text
point_id
source_id             flow, schedule, operator, safety, integration, etc.
command_class         normal, schedule, integration, operator, protection, etc.
priority              precedence within the command policy
value
issued_at
expires_at            optional
reason                optional operator/audit note
correlation_id        for tracing a flow execution
```

The command policy orders command classes first and numeric priority second. A
practical default, from highest to lowest precedence, is:

1. emergency interlock;
2. equipment protection;
3. operator override;
4. plant sequencing;
5. external integration;
6. schedule;
7. normal flow control.

The numeric priority resolves conflicts between authorised sources in the same
class. A deterministic tie-breaker, such as most recent command followed by
stable source identifier, ensures that restart or event ordering does not
produce an unpredictable result.

The classes and their ordering are site policy rather than hard-coded point
behaviour. An installation may insert additional classes or disallow some
classes entirely. Permissions determine which actors may use each class.
Ordinary flow nodes use normal flow control unless explicitly authorised for a
higher class; a user-supplied priority alone cannot elevate a command into a
protected class.

Releasing a command removes only that source's entry in the specified command
class and priority. The point then exposes the next highest command. If no
commands remain, it uses its **relinquish default** or releases control to the
external binding, according to configuration. “Release all” is a privileged
commissioning action because it can also remove commands placed by protection
and sequencing logic.

Commands should optionally expire, especially browser/operator overrides. The
UI should encourage a duration and clearly distinguish a command that survives
restart from one that does not.

## Input processing

Bound inputs commonly need a pipeline with practical
separation of field input, conditioning, and control use:

```text
driver sample
  -> electrical/protocol validation
  -> scaling and unit conversion
  -> calibration offset
  -> filtering
  -> range and plausibility checks
  -> source value, quality, and timestamp
  -> out-of-service substitution (if active)
  -> present value
  -> alarm and change-of-value evaluation
  -> flow event
```

Configuration may include:

- raw and engineering ranges;
- linear or driver-specific scaling;
- calibration offset/gain;
- deadband and change-of-value threshold;
- debounce for contacts;
- filter time constant or sample window;
- stale timeout;
- open-circuit, short-circuit, over-range, and under-range detection;
- polarity and normal-state selection; and
- fallback policy.

A bad input must not silently become zero. Depending on point configuration, a
consumer can receive the bad quality, hold the last good value for a bounded
time, use a declared fallback, or inhibit the affected flow. Whichever policy is
used must remain visible in the point quality and audit trail.

## Output processing and feedback

An output write follows:

```text
commands
  -> priority arbitration
  -> type/range validation
  -> rate and equipment-protection constraints
  -> out-of-service/disabled policy
  -> conversion and driver write
  -> write status and optional readback
```

For analog outputs, range behaviour must be configured as reject, clamp, or
fault. For digital plant, optional minimum on/off times prevent short cycling.
For mutually exclusive outputs, such as heat/cool or actuator
increase/decrease, interlocking belongs close to the output driver or a reusable
equipment-protection function rather than relying only on a user flow.

The requested value, value sent to the binding, and actual feedback are
different facts. A valve commanded to 100% is not necessarily open. Where an
external system supplies readback, expose it as a related input point and allow
deviation alarming.

## Quality and reliability

Quality is independent of the value. Recommended top-level quality values are:

- **Good**: value is current and passed validation.
- **Uncertain**: usable only according to site policy, for example stale,
  substituted, awaiting confirmation, or derived from an uncertain input.
- **Bad**: should not be used for normal control, for example communication
  failure, sensor fault, disabled, invalid configuration, or type mismatch.

The reason should be machine-readable and may include:

```text
reliable
stale
communication_failure
sensor_open
sensor_short
over_range
under_range
invalid_value
disabled
out_of_service_simulated
write_failed
binding_not_configured
dependency_unreliable
```

Flows should propagate quality. A calculated point is normally no better than
the inputs required to calculate it, although a node may define an explicit
fallback rule. Every fallback should be observable rather than falsely marking
the result as an ordinary good measurement.

## Alarms and trends

Alarming and trending are optional services attached to a point, not separate
copies of the point.

Analog alarms may define high/low warning and critical limits, deadband, delay,
and acknowledgement requirements. Digital and multi-state alarms identify
which states are abnormal and for how long they must persist. Fault and stale
conditions can generate technical alarms independently of process limits.

Each alarm transition records point, value, quality, limit/state, timestamp,
acknowledgement, and message. Alarm evaluation should distinguish:

- normal;
- off-normal/alarm;
- returned to normal but unacknowledged; and
- fault/unreliable.

Trend configuration defines periodic or change-of-value sampling, retention,
and whether value, quality, effective command source, and overrides are stored.
Command and override history belongs in an immutable audit log even when value
trending is disabled.

## Flow integration

The frontend presents points as typed graph endpoints:

- a **Read Point** node emits value, quality, and timestamp;
- a **Write Point** node submits a command with a declared priority/source;
- a **Point Changed** trigger runs on a change-of-value event;
- **Release Point Command** relinquishes only the calling flow's command;
- quality-aware nodes allow fallback, inhibit, or alarm behaviour.

Graph validation should catch incompatible data types and units before
deployment. A flow may read and write the same virtual point, but the designer
should detect direct feedback cycles and require an explicit delay, latch,
deadband, or stateful control block.

At runtime, every deployed flow has a stable command source identifier. Stopping
or undeploying a flow releases its commands unless configuration explicitly
marks them as retained. This prevents an output from remaining indefinitely at
the last command of a flow that no longer exists.

Point updates should be event-driven where possible, with periodic evaluation
available for control loops and bindings that are polled. Events include the
complete runtime envelope rather than the value alone.

## Example points

### Supply-air temperature

```text
implementation: bound
direction: input
value_type: analog
units: degC
binding: controller AI-3
range: -20 to 80 degC
stale_timeout: 30 seconds
```

The point scales the field value, reports sensor open/short as bad quality,
trends every five minutes and on a 0.5 °C change, and generates high/low alarms.
A commissioning engineer can take it out of service and inject a simulated
temperature without disabling the flows that consume it.

### Occupied cooling setpoint

```text
implementation: virtual
direction: value
value_type: analog
units: degC
commandable: true
relinquish_default: 24
persistence: retained
```

Normal control writes in the normal class, a schedule may reset it in the
schedule class, and an operator may temporarily command it in the operator
override class. All requests remain visible, but the present value is selected
by the configured command policy.

### Pump enable

```text
implementation: bound
direction: output
value_type: digital
states: stopped/running
binding: controller DO-2
minimum_off_time: 120 seconds
safe_disable_policy: relinquish
```

A normal flow requests run in the normal class. Equipment protection can stop
it in the protection class, and a temporary operator command uses the override
class. A separate pump-status input confirms operation; a mismatch alarm is
raised if command and feedback disagree after the start delay.

### Open-collector alarm output

```text
implementation: bound
direction: output
value_type: digital
states: released/asserted
electrical_mode: open_collector
true_drive: pull_low
safe_disable_policy: high_impedance
```

The logical point remains boolean. When released, the output is high impedance;
the system does not infer the external circuit voltage without a separate
feedback input.

## Backend and persistence expectations

The backend should keep point definitions, current runtime state, commands, and
history in separate stores or records. Definition updates are validated and
versioned. Runtime updates use optimistic revision numbers or monotonic sequence
numbers so clients can order events and reject stale writes.

On restart:

- retained virtual values are restored;
- volatile values return to their configured defaults;
- only commands explicitly configured for restart retention are restored;
- bound inputs remain uncertain/bad until refreshed;
- outputs reconcile the command table before any write; and
- drivers report connection state rather than presenting cached data as live.

Writes should be idempotent where possible. APIs should support subscribing to
point changes, reading a consistent snapshot, commanding with priority and
optional expiry, relinquishing a command, and viewing command/audit history.

## Frontend expectations

The point manager should show, at a glance:

- name, value, units, and state text;
- quality and last-update age;
- implementation and binding;
- in-service, alarm, and override indicators;
- effective command source and priority; and
- read/write capability.

Point detail should separate configuration from live commissioning. A
commissioning view should expose raw/source/present values, scaling, quality
reason, binding health, command table, and readback. Potentially hazardous
actions—taking a point out of service, persistent override, release all, or
commanding high-priority plant—require appropriate authorisation and an audit
reason.

The flow designer filters available points by compatible direction, type, and
units and makes simulated, stale, overridden, or unreliable values visually
obvious.

## Safety and operational rules

- No failed or missing value is silently converted to zero, `false`, or an
  empty string.
- An output has an explicit startup, shutdown, communication-loss, and
  disable policy.
- Safety commands cannot be issued or released by ordinary flows.
- Operator overrides are visible, attributable, auditable, and preferably
  time-limited.
- Removing or stopping a flow relinquishes its non-retained commands.
- Taking a point out of service never silently operates field equipment.
- Configuration changes that alter units, polarity, scaling, or safe state are
  validated and audited.
- Control logic can always inspect value quality and must define how unreliable
  required inputs are handled.

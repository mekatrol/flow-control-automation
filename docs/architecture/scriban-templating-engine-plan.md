# Scriban templating engine implementation plan

## Purpose

Add a small, transport-independent templating service that transforms an
explicit input context into text. A read adapter can use it to select a point
value from a normalized HTTP, JSON, or MQTT input, while a write adapter can use
it to render a payload or topic. Scriban supplies the template parser and
renderer; the application owns the public contract, allowed input values, error
semantics, dependency injection, and tests.

These phases implement the generic transformation engine and prove both
extraction-style and payload-generation templates. Transport acquisition,
publishing, and conversion of rendered read text into the configured point type
remain responsibilities of their respective adapters.

## Implementation status (2026-09-12)

Phases 0, 1, and 2 are complete. Phase 3 remains pending.

### Completed

- Scriban 7.4.0 is pinned in `Server.Services`; `Server.Common` has no Scriban
  dependency.
- `ITemplateService`, `TemplateValidationResult`, `TemplateDiagnostic`,
  `TemplateError`, and `TemplateRenderException` provide the application-owned
  contract and failure vocabulary.
- The internal `ScribanTemplateService` validates and renders templates from an
  explicitly supplied, case-sensitive value map.
- Render calls use fresh globals, output, and `TemplateContext` instances while
  the stateless service is registered as a singleton.
- The runtime uses strict lookup, invariant culture, bounded loops, recursion,
  regex execution, string conversion, and total output.
- Rich application objects and dictionaries with non-string keys are rejected.
  Supported dictionaries, sequences, JSON nodes, and scalar values are copied
  into Scriban-owned structures.
- Includes, dynamic object evaluation, and clock access are removed from the
  available Scriban built-ins.
- The reserved `json` filter serializes values with `System.Text.Json` so JSON
  payload templates correctly handle strings, numbers, booleans, and null.
- `AddTemplatingServices` registers `ITemplateService` through the existing
  `AddServerServices` composition root.
- Initial unit coverage verifies DI lifetime, invalid-template diagnostics,
  JSON rendering, missing-value classification, and architecture boundaries.
- Positive and negative YAML fixture catalogues cover literal and nested value
  rendering, JSON payload generation and escaping, conditionals, invariant
  formatting, syntax and runtime failures, and execution and output limits.
- The test fixture loader discovers fixtures deterministically, rejects malformed
  YAML and duplicate keys or names, validates the fixture envelope, and loads
  fixtures from the test output directory.

### Verification completed

- The focused templating and service-boundary suite passed: 13 tests.
- The full backend unit suite passed: 340 tests.
- The backend solution build succeeded with zero warnings and zero errors.
- `git diff --check` completed without whitespace errors.

### Remaining

- Phase 3: add comprehensive fixture-driven validation and rendering tests,
  plus the remaining boundary, isolation, concurrency, culture, and limit tests.

## Required outcomes

1. Application code depends on an application-owned interface rather than on
   Scriban types.
2. The implementation parses and renders Scriban templates using only an
   explicitly supplied variable map.
3. The implementation is registered by the existing server-service dependency
   injection extension pattern.
4. Positive and negative behavior is described by YAML fixtures.
5. Every fixture is parsed as YAML before its template is compiled or rendered.
6. Tests distinguish invalid YAML, invalid Scriban, and Scriban render failures.
7. Multiline templates, including JSON payloads, remain readable in fixtures.

## Scope and boundaries

The templating service:

- accepts template text and a case-sensitive map of safe values;
- validates Scriban syntax;
- renders text using invariant culture;
- reports stable application-level errors with Scriban diagnostic details;
- creates a new Scriban execution context for every render;
- does not perform network access, publish MQTT messages, or issue HTTP calls;
- does not acquire a transport response or convert rendered text into a typed
  point value;
- does not expose service providers, entities, transport clients, file access,
  reflection helpers, or other rich application objects to a template.

MQTT and HTTP services remain responsible for building their own safe context,
calling the templating service, validating protocol-specific output, and then
sending it. For example, an MQTT JSON publisher should validate the rendered
text with `JsonDocument.Parse` before publishing it. JSON validation does not
belong in the generic service because plain-text payloads are also valid.

## Proposed contract

Place the public contract under
`backend/Server/Server.Common/Contracts/Templating` and its implementation
under `backend/Server/Server.Services/Templating`.

```csharp
public interface ITemplateService
{
    TemplateValidationResult Validate(string template);

    string Render(
        string template,
        IReadOnlyDictionary<string, object?> values);
}
```

`Validate` supports configuration validation without rendering. It returns an
application-owned result containing zero or more diagnostics. Each diagnostic
should contain a stable category plus message, line, and column where Scriban
provides them.

`Render` returns rendered text. It throws an application-owned
`TemplateRenderException` for expected template failures. The exception should
carry a stable category such as `InvalidTemplate`, `MissingValue`,
`ExecutionLimitExceeded`, or `OutputLimitExceeded`; callers must not need to
catch Scriban exceptions or inspect Scriban message text.

The initial contract intentionally takes a dictionary instead of a point- or
MQTT-specific request. A transport builds the smallest context it needs:

```csharp
var values = new Dictionary<string, object?>
{
    ["value"] = 21.75m,
    ["timestamp"] = "2026-09-12T03:15:00Z",
    ["point_id"] = "room-temperature"
};
```

Do not expose a public compile/cache abstraction in the first increment.
Compilation caching can be added internally after profiling, keyed by the exact
template text. Cached parsed templates may be shared only if Scriban documents
them as safe for concurrent rendering; `TemplateContext` and globals must never
be shared between calls.

## Scriban runtime policy

The implementation should centralize context creation so its safety and
compatibility rules cannot drift between callers:

- use strict variable/member handling so misspelled or missing values fail;
- use invariant culture for numeric and date formatting;
- expose values through a new `ScriptObject`, not by importing arbitrary .NET
  application objects;
- do not configure a template loader or include/import support;
- do not expose dynamic evaluation, reflection, filesystem, network, clock, or
  environment access;
- configure bounded loop/recursion execution and bounded output where supported
  by the selected Scriban version;
- translate parse and runtime diagnostics at the service boundary.

JSON string escaping must not be implemented with quoted interpolation such as
`"{{ value }}"`. During Phase 1, expose one narrowly scoped helper, with its
name confirmed against Scriban conventions, that delegates to
`System.Text.Json.JsonSerializer.Serialize`. The intended template form is:

```scriban
{"value": {{ value | json }}}
```

This preserves JSON strings, booleans, numbers, and null correctly. Add tests
before treating the helper name and behavior as public configuration syntax.

## YAML fixture format

YAML is preferable to JSON for these fixtures. YAML literal block scalars make
multiline templates natural and avoid escaping every JSON quote. Use `|-` so
the fixture does not add a trailing newline to the template:

```yaml
name: mqtt-json-numeric-value
template: |-
  {
    "value": {{ value | json }},
    "source": {{ source | json }}
  }
values:
  value: 21.75
  source: room-temperature
expected: |-
  {
    "value": 21.75,
    "source": "room-temperature"
  }
```

Scriban delimiters and JSON braces have no special meaning inside a YAML
literal block. The indentation common to the block is removed by YAML, while
the internal indentation and newlines are retained.

Use a small typed fixture envelope for the test runner even though production
YAML schemas will vary:

```csharp
internal sealed record TemplateFixture
{
    public required string Name { get; init; }
    public required string Template { get; init; }
    public Dictionary<string, object?> Values { get; init; } = [];
    public string? Expected { get; init; }
    public string? ExpectedError { get; init; }
}
```

This envelope is test metadata, not a proposed production configuration schema.
It proves that a property named `template` can survive YAML parsing unchanged.
If production code needs to find templates inside arbitrary YAML later, that
schema-specific traversal belongs to its configuration parser, not to
`ITemplateService`.

Store fixtures under:

```text
testdata/templates/
  positive/
  negative/
```

Add this tree to `Tests.Unit.csproj` as linked content copied to the test output,
following the existing `testdata/contracts` fixture pattern. Tests must load
from `TestContext.CurrentContext.TestDirectory`, not from assumptions about the
repository working directory.

## Delivery phases

### Phase 0 — Add Scriban (complete)

1. Add a pinned `Scriban` `PackageReference` to
   `Server.Services/Server.Services.csproj` using `dotnet add package Scriban`.
2. Keep Scriban out of `Server.Common`; its public contracts must contain no
   Scriban types.
3. Restore and build the backend solution.
4. Commit the project-file and restore metadata changes produced by the normal
   repository workflow.

Acceptance criteria:

- the backend restores and builds with zero warnings;
- only `Server.Services` has a direct runtime dependency on Scriban;
- the selected package version is explicit and compatible with `net10.0`.

### Phase 1 — Contract, implementation, and dependency injection (complete)

1. Add `ITemplateService`, `TemplateValidationResult`, diagnostic/error enums,
   and `TemplateRenderException` to `Server.Common`.
2. Add an internal `ScribanTemplateService` implementation to
   `Server.Services/Templating`.
3. Implement null/empty argument checks, syntax validation, strict lookup,
   invariant formatting, a fresh restricted context per render, the JSON
   serialization helper, diagnostic translation, and execution/output limits.
4. Add an internal
   `TemplatingServiceCollectionExtensions.AddTemplatingServices` extension in
   `Server.Services/ServiceExtensions`.
5. Register `ITemplateService` as a singleton if the implementation contains no
   mutable per-render state. All render state must remain method-local.
6. Call `AddTemplatingServices()` from the existing public
   `AddServerServices` composition root.
7. Add a DI resolution test using the existing `TestServices.CreateProvider`
   helper.

Acceptance criteria:

- consumers reference only `ITemplateService` and application-owned result and
  exception types;
- a missing variable fails instead of silently rendering empty text;
- concurrent calls cannot share values or execution state;
- a provider built by `AddServerServices` resolves exactly one
  `ITemplateService`;
- no transport dependency is introduced into the templating implementation.

### Phase 2 — Positive and negative YAML fixtures (complete)

1. Add a fixture loader in `Tests.Unit/Templating` using the repository's
   existing YamlDotNet dependency and conventions.
2. Parse each file into `TemplateFixture` before invoking the service.
3. Reject malformed fixture YAML, duplicate keys, missing `name`, missing
   `template`, a fixture declaring both/neither `expected` and
   `expectedError`, and duplicate fixture names.
4. Add positive fixtures for:
   - literal text;
   - scalar substitution;
   - extracting a scalar from a normalized nested input context;
   - a multiline MQTT JSON payload;
   - numeric, boolean, string, and null JSON values;
   - quotes, backslashes, newlines, and Unicode through the JSON helper;
   - conditionals;
   - nested dictionary/array access if this is part of the supported context;
   - invariant decimal formatting under a non-English test culture.
5. Add negative fixtures for:
   - unmatched or malformed Scriban delimiters;
   - unknown variables;
   - invalid member/index access;
   - a disallowed function or capability;
   - an execution-limit breach;
   - an output-limit breach;
   - malformed fixture YAML, kept separately when it cannot deserialize to the
     normal envelope.

Acceptance criteria:

- every template-bearing fixture is syntactically valid YAML;
- YAML parsing preserves the exact multiline template expected by the test;
- fixtures describe observable results or stable error categories, not Scriban
  exception messages;
- positive and negative fixture discovery is deterministic.

### Phase 3 — Fixture-driven unit tests (pending)

1. Add `ScribanTemplateServiceTests` using NUnit
   `TestCaseSource`, matching the existing test-suite style.
2. Run every positive fixture through YAML deserialization, template
   validation, and rendering; compare output exactly, including whitespace.
3. Run every negative template fixture through YAML deserialization and then
   validation or rendering; assert the application error category and useful
   diagnostic location where applicable.
4. Test malformed YAML in fixture-loader tests, separately from Scriban
   failures, so the source of failure is unambiguous.
5. Add direct boundary tests that are clearer in C# than YAML: null arguments,
   DI lifetime/resolution, repeated render isolation, and concurrent rendering.
6. Run the focused templating tests, the full backend unit suite, and the
   backend build.

Acceptance criteria:

- all positive fixtures validate and render their exact expected output;
- all negative fixtures fail with their exact stable application error category;
- malformed YAML never reaches Scriban;
- invalid Scriban is not reported as invalid YAML;
- tests pass regardless of current directory or machine culture;
- the full backend unit suite and zero-warning build pass.

## Suggested verification commands

Run from `backend/Server`:

```powershell
dotnet build
dotnet test Tests.Unit/Tests.Unit.csproj --no-restore --filter FullyQualifiedName~Tests.Unit.Templating
dotnet test Tests.Unit/Tests.Unit.csproj --no-restore
```

## Deferred work

Defer these until the rendering contract has real transport consumers:

- template compilation caching and cache eviction;
- asynchronous custom functions;
- protocol-specific validation and encoding;
- rendering MQTT topics separately from payloads;
- transport-specific context builders for raw text, JSON, HTTP metadata, and
  MQTT topics;
- versioning the production YAML configuration vocabulary;
- metrics for compile/render duration and failure categories.

If text-to-type conversion proves lossy for read templates, add a separate
application-owned typed evaluation operation rather than changing `Render` to
return Scriban runtime values. Keep acquisition, extraction, conversion, and
write rendering as explicit stages even if Scriban participates in more than
one stage.

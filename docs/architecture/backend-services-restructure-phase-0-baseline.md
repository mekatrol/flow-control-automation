# Backend services restructure Phase 0 baseline

Captured on 2026-09-10 before production namespaces or files were moved.

## Test baseline

- Backend: `dotnet test Server.slnx --no-restore` — 309 passed, 0 failed,
  0 skipped.
- Frontend contract/unit suite: `npm.cmd run test:unit -- --run` — 62 test
  files and 243 tests passed.

## Project dependency baseline

```text
Server.Common
  (no project references)
Server.Compiler -> Server.Common
Server.Data
  (no project references)
Server.Services -> Server.Common, Server.Compiler, Server.Data
Server.Api -> Server.Services
Tests.Unit -> Server.Api, Server.Common, Server.Compiler, Server.Data,
              Server.Services
```

This matches the existing dependency direction documented by the restructure
plan. Phase 2 must add the Common contract dependency to API before its direct
Services contract usage can be removed.

## Public-symbol and consumer inventory

The Phase 0 source inventory contains 143 public declarations in
`Server.Services`: 70 under `Contracts`, 14 under `Implementation`, and 59 at
the project root (interfaces, exceptions, options, and the DI entry point).
The complete symbol classification and target homes are the plan's
"Public contract classification", "Visibility cleanup", and "File
allocation" sections. The source-of-truth inventory can be reproduced without
stale generated data with:

```powershell
rg -n "^public (?:sealed |static |abstract |partial )*(?:class|record|interface|enum|struct)|^public interface|^public enum" backend/Server/Server.Services -g '*.cs'
```

Consumer audit:

- `Server.Api` consumes root service interfaces/exceptions, service contract
  models, and the public `AddServerServices` extension.
- `Tests.Unit` consumes the same public surface and also directly names the 14
  public implementation declarations. Those direct consumers are the tests
  identified for conversion in the plan's visibility-cleanup phases.
- `Server.Compiler` and `Server.Data` do not reference `Server.Services`.
- No other production project constructs a type from
  `Server.Services.Implementation`.

## Namespace-sensitive serialization gate

The audit found no type-name or namespace-bearing serialization (`$type`,
assembly-qualified names, custom serialization binders, or polymorphic type
discriminators). JSON uses explicit camel-case names, case-sensitive property
matching, and string enums. YAML validation uses explicit current field sets.
Consequently later namespace moves have no pending namespace-sensitive wire or
persistence behavior.


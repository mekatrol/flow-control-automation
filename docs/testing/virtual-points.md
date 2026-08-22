# Virtual-point verification

Virtual points are verified at domain, compiler, runtime, frontend, API, and
controller boundaries. Test counts are deliberately omitted because they change
as coverage grows.

## Domain and API coverage

- Equal keys are isolated across execution instances.
- Compatible declarations unify on one instance.
- Type, units, persistence, default, and capability conflicts are rejected.
- Virtual definitions reject physical source mappings and invalid directions.
- Qualified resolution cannot return a point from another context or instance.
- Authentication, scoped authorization, and actor-qualified auditing are
  enforced.
- Retained backup, clear, exact-contract restore, and resource limits are
  covered.

## Compiler and deployment coverage

- Each point node resolves only a compatible contract.
- Missing manually entered keys produce a specific diagnostic.
- Stale revisions, disabled instances, missing bindings, unsupported target
  capabilities, and resource limits prevent activation.
- A second writer on one instance is rejected and identifies the owner.
- The same writer key on another instance is independent.
- A failed multi-program preparation leaves the prior generation active.

## Runtime and concurrency coverage

- One program's committed output becomes visible to another on the next context
  scan.
- Analog and digital values retain type, quality, timestamp, and version.
- Defaults and unavailable initial quality are deterministic.
- Volatile reset and retained restoration affect only the owning instance.
- Failed execution cannot partially commit an output set.
- Undeployment and disabling release writer ownership.
- Forced scan, reset, restore, deployment, and inspection interleavings do not
  produce torn records, deadlocks, or cross-instance leakage.

## Frontend coverage

- Lookup filters by value type and capability and labels physical and virtual
  choices.
- Search, manual entry, and selection remain keyboard accessible.
- Debounced and blur/save validation distinguish missing points from service
  failures.
- Context changes revalidate without silently replacing keys.
- Invalid mappings block save and deployment.
- The create-virtual-point workflow selects the new declaration.

## Acceptance scenario

1. Create two controller instances from the same template.
2. Create one context containing writer and reader programs that declare the
   same retained analog key.
3. Deploy that context to both instances.
4. Confirm next-scan visibility between programs on each instance.
5. Confirm values remain independent between instances.
6. Restart one instance and confirm retained restoration.
7. Attempt a second writer and confirm a structured conflict.
8. Enter an unknown key in the designer and confirm save and deployment remain
   blocked.

## Commands

From the repository root:

```sh
dotnet test backend/Server/Server.slnx --no-restore
```

From `frontend/flow-control-ui`:

```sh
npm run test:unit -- --run
npm run test:e2e
npm run test:e2e:dotnet
npm run build
npm run lint:check
npm run format:check
```

Controller host tests are documented in
[controller development](../development/controller.md#host-tests).


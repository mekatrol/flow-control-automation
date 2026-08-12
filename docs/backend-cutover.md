# .NET backend cutover and rollback

The supported backend is `backend/Server/Server.Api`. Do not remove the Go
implementation until this runbook has been exercised on the target deployment,
the canary has passed, and the agreed rollback window has elapsed.

## Required configuration

- `SERVER_ADDRESS`: listening URL, for example `http://0.0.0.0:8080`.
- `ConnectionStrings__FlowControl`: SQLite connection string.
- `CREDENTIAL_ENCRYPTION_KEY`: Base64 for exactly 32 bytes, supplied by a secret
  manager and retained for rollback/recovery.

The database directory must exist and be writable by the service account. Never
print the encryption key or resolved credential values in verification output.

## Pre-cutover verification

From a clean checkout:

```sh
dotnet build backend/Server/Server.slnx
dotnet test backend/Server/Server.slnx
dotnet format backend/Server/Server.slnx --verify-no-changes
cd frontend/flow-control-ui
npm ci
npm run format:check
npm run lint:check
npm run test:unit -- --run
npm run build
npm run test:e2e:dotnet
```

Back up the existing Go data directory before changing the service command:

```sh
install -d -m 0700 /path/to/backups/pre-dotnet-cutover
cp -a /path/to/go/data/. /path/to/backups/pre-dotnet-cutover/go-data/
```

If an existing .NET SQLite database is being replaced, stop the service and
back up the database plus WAL/SHM sidecars together:

```sh
cp -a /path/to/flow-control.db* /path/to/backups/pre-dotnet-cutover/
```

Record the last known-good Go release identifier and deployment configuration.
Confirm that restoring the backup into a staging instance starts that release
successfully before proceeding.

## Cutover

1. Stop writes and stop the Go service.
2. Take the final backup described above.
3. Provision the .NET SQLite database and encryption key.
4. Configure the service command:

   ```sh
   dotnet Server.Api.dll
   ```

5. Start one canary instance and keep the Go deployment stopped but available
   for rollback.
6. Run the smoke checks below through the same reverse proxy used by clients.

No automatic importer from the Go JSON stores is currently provided. A
deployment containing data that must be retained must use a separately reviewed
and tested import procedure before production cutover.

## Canary smoke checks

Use non-secret canary records. Do not include real credential values in shell
history, logs, screenshots, or captured responses.

1. `GET /api/health` returns `{"status":"ok"}`.
2. List flows, create/save a canary flow, deploy it, and verify its runtime is
   `running`. Until portable Flow IL server-host Phase 4 is complete, this checks
   lifecycle/API wiring only; production cutover of automation execution also
   requires a compiled flow to produce the expected VM tick snapshot.
3. List point sources and create/read a canary source.
4. Create a canary credential and verify create/get/list return metadata only.
5. Verify referenced-credential deletion is rejected.
6. Stop the canary gracefully while a cancellable connectivity request is
   outstanding and confirm shutdown completes without an unhandled exception.

Promote the .NET deployment only after the frontend and automated suites pass
against the canary.

## Rollback

1. Stop the .NET service and preserve its SQLite database for diagnosis.
2. Restore the pre-cutover Go data backup and its original encryption key with
   restrictive permissions.
3. Restore the recorded Go release and service configuration.
4. Start the Go service and repeat health, flow list, source list, and credential
   metadata smoke checks.
5. Keep post-cutover writes quarantined. Do not attempt to merge SQLite and Go
   JSON data without a separately reviewed reconciliation procedure.

Document the cutover time, canary evidence, rollback deadline, operators, backup
location, and final decision. Phase 9 may begin only after that deadline passes.

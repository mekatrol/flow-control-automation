# Virtual-point operations

This guide covers production controls for shared virtual-point state. Functional
behavior is described in the [virtual-points guide](../guides/virtual-points.md),
and routes are listed in the [API reference](../reference/virtual-points-api.md).

## Authorization

Production API startup requires at least one configured identity with a key and
permissions. Clients send the key through `X-Api-Key`. Keep keys in the
deployment secret manager rather than source control or browser configuration
files.

Permissions relevant to virtual points are:

- `contexts.view` and `contexts.edit`
- `deployments.manage`
- `points.view`, `points.edit`, and `points.command`
- `points.manage-retained`
- `system.view` and `system.manage`

The wildcard permission `*` is intended only for deliberately configured
administrative identities.

### Generate an API key

Generate a cryptographically random 32-byte key for each API identity. Run the
command for your operating system, then copy the printed value into your secret
manager or local configuration.

Windows PowerShell:

```powershell
$key = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Fill($key)
[Convert]::ToBase64String($key)
```

macOS or Linux with OpenSSL:

```sh
openssl rand -base64 32
```

For local development, place the generated value in the gitignored
`backend/Server/Server.Api/appsettings.Local.json` file:

```json
{
  "ApiAccess": {
    "Identities": {
      "local-admin": {
        "Key": "REPLACE_WITH_GENERATED_API_KEY",
        "Permissions": ["*"]
      }
    }
  }
}
```

For a deployment, configure the same identity through the deployment secret
manager. The corresponding environment variables are:

```text
ApiAccess__Identities__local-admin__Key
ApiAccess__Identities__local-admin__Permissions__0
```

Set the permission variable to `*` only for an intentionally administrative
identity. Do not commit API keys to source control, reuse them between
identities, or expose them in browser configuration files. Clients provide the
generated value in the `X-Api-Key` request header.

## Capacity

The service rejects configurations that exceed:

- 128 virtual points in one execution context.
- 64 retained virtual points in one execution context.
- 128 allocated virtual points on one execution instance.

These are admission limits, not targets. Leave capacity for later context
changes and confirm the selected controller advertises the required virtual
point and retention capabilities.

## Retained-state lifecycle

Retained state is keyed by execution instance and point key. Stored metadata
includes the resolved contract and version. Restoration succeeds only when the
execution instance and complete contract match exactly, including type, units,
persistence, and default.

Removing a declaration does not silently destroy its retained value. It remains
available for a later exactly compatible declaration until an authorized
operator clears it. There is no migration or coercion between contracts.

## Backup and restore

Retained-state backups are separate from configuration backups.

1. Export the instance-qualified backup before maintenance or replacement.
2. Preserve its schema version and execution-instance identity.
3. Restore only to that same instance identity after compatible declarations
   have been activated.
4. Inspect runtime values and quality after restoration.

Restore is atomic from the runtime's perspective. An incompatible entry rejects
the operation rather than being converted or partially applied.

## Clearing state

Clearing retained state is privileged and explicit. It removes persisted values
for the selected instance and returns active retained cells to their default or
unavailable state. Export a backup first when recovery might be required.

Volatile state resets when its execution instance restarts. It is not included
in retained backups.

## Observability and audit

Runtime inspection exposes:

- Typed value and data quality.
- Timestamp and monotonically increasing version.
- Active writer and registered readers.
- Persistence status.

Configuration and state mutations produce durable actor-qualified audit
records. Do not copy credentials or sensitive physical mappings into point
names, values, logs, or operational notes.

## Controller identity and reconnects

Deployment and command exchange use concrete execution-instance identity, not
only controller-template identity. A controller must reject a deployment or
command intended for another installation even when both devices use the same
template.

After a disconnect, treat cached physical values according to their quality and
timestamp. Virtual-point values remain instance-local; retained values restore
before affected programs activate, and volatile values follow the instance
restart policy.

# Database migrations

### Database migrations

The backend uses EF Core migrations in `Server.Data/Migrations`.
Run the commands below from `backend/Server` with the .NET 10 SDK installed.
Install the EF CLI at the version used by `Server.Data.csproj` (currently
`10.0.10`):

```sh
dotnet tool install --global dotnet-ef --version 10.0.10
```

If already installed, use `dotnet tool update --global dotnet-ef --version 10.0.10`.
Configure local API settings as described above before using the startup project.
The startup project, `Server.Api`, must directly reference
`Microsoft.EntityFrameworkCore.Design` at the same version as `Server.Data`;
the private package reference in `Server.Data` does not flow to the startup project.

#### Adding a migration

Update the entities and model configuration in `Server.Data`, then generate a
migration with a descriptive name (replace `AddExampleField`):

```sh
dotnet ef migrations add AddExampleField --project Server.Data --startup-project Server.Api --context FlowControlDbContext
```

Review the generated `Up` and `Down` methods and model snapshot, especially
renames or operations that could discard data. Commit the migration, its
`.Designer.cs` metadata file, and `FlowControlDbContextModelSnapshot.cs` together.
See Microsoft's [migration management guide](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)
for details on reviewing generated migrations.

The API applies pending migrations on startup and then creates the SQLite
row-version triggers. To apply just the migrations explicitly, replace the
example absolute database path below with your development database path:

```sh
dotnet ef database update --project Server.Data --startup-project Server.Api --context FlowControlDbContext --connection "Data Source=D:/path/to/flow-control.db"
```

Use the database selected by `ConnectionStrings:FlowControl`, including any
override in `appsettings.Local.json`. Relative SQLite paths depend on the
process working directory; an absolute path avoids updating a different file.
Start the API afterward to initialize its triggers. Run the backend checks
listed below before committing.

#### Resetting to a single initial migration for the first release

This pre-release reset discards existing databases and migration history. There
is no legacy upgrade or data-preservation support: every installation using the
old migrations must recreate its database. After the first release, preserve
released migrations and add new ones for schema changes.

1. Stop the API and any other processes using the database. Resolve the actual
   SQLite file from `ConnectionStrings:FlowControl` and delete that database and
   any matching `-wal` and `-shm` files. This deletes all stored application data.
2. Delete all `.cs` files directly inside
   `Server.Data/Migrations`, including old migration files,
   `.Designer.cs` files, and `FlowControlDbContextModelSnapshot.cs`. Keep the
   directory's `README.md`. Removing the snapshot lets EF generate the entire
   current schema instead of a difference from the old model.
   Remove any now-unused `Microsoft.EntityFrameworkCore.Infrastructure` and
   `Microsoft.EntityFrameworkCore.Migrations` global usings from
   `Server.Data/GlobalUsings.cs` so `IDE0005` does not block the build. Generated
   migrations include their own using directives.
3. Generate the new baseline from the current model:

   ```sh
   dotnet ef migrations add InitialCreate --project Server.Data --startup-project Server.Api --context FlowControlDbContext --output-dir Migrations
   ```

4. Review the baseline to ensure it creates all current tables and indexes.
   Retain any custom SQL needed by a fresh installation; legacy data conversion
   SQL is unnecessary. Row-version triggers remain in `InitializeDatabase`.
   Preserve the custom `InsertData` for the built-in `server` execution instance,
   using `"executionInstanceType":"server"` in its JSON. EF scaffolding does not
   regenerate this manually added seed row from the model.
   A single EF migration includes `InitialCreate.cs` and its `.Designer.cs`
   metadata (both timestamp-prefixed), plus the model snapshot; keep all three.
5. Start the API against the empty database to apply the baseline and initialize
   triggers. Run the checks below, and confirm `migrations list` reports only
   the new `InitialCreate` migration:

   ```sh
   dotnet format Server.slnx --verify-no-changes
   dotnet build Server.slnx
   dotnet test Server.slnx
   dotnet ef migrations list --project Server.Data --startup-project Server.Api --context FlowControlDbContext --connection "Data Source=D:/path/to/flow-control.db"
   ```

   Replace the example path with the same database initialized by the API.
6. Commit the old migration deletions, new baseline and snapshot, and test
   cleanup together. Recreate every other pre-release database before running
   this version against it.

# Database migrations

Create future migrations from `backend/Server` with:

```sh
dotnet ef migrations add <Name> \
  --project Server.Data/Server.Data.csproj \
  --startup-project Server.Api/Server.Api.csproj \
  --context FlowControlDbContext
```

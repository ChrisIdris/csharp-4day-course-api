# BankingApi - PostgreSQL Setup Guide

This guide will help you set up PostgreSQL using Docker Compose and configure Entity Framework Core migrations for the BankingApi project.

## Prerequisites

- Docker Desktop installed and running
- .NET 10.0 SDK installed
- Terminal (PowerShell, CMD, or bash)

## Step 1: Start PostgreSQL with Docker Compose

Navigate to the BankingApi directory and start the PostgreSQL container:

```powershell
cd exercises\BankingApi
docker compose up -d
```

Verify the container is running:

```powershell
docker compose ps
```

You should see the `bankingapi-postgres` container with status "Up" and healthy.

To view logs:

```powershell
docker compose logs db
```

## Step 2: Restore NuGet Packages

Restore the packages (including the new Npgsql.EntityFrameworkCore.PostgreSQL):

```powershell
dotnet restore
```

## Step 3: Create and Apply Migrations

### Create Initial Migration

From the `exercises\BankingApi` directory, create the initial migration:

```powershell
dotnet ef migrations add InitialCreate
```

This will create a `Migrations` folder with the migration files.

### Apply Migration to Database

Apply the migration to create the database schema:

```powershell
dotnet ef database update
```

This will create the `Accounts`, `Branches`, and `Customers` tables in the PostgreSQL database.

## Step 4: Run the Application

Start the application:

```powershell
dotnet run
```

The API should now be running and connected to PostgreSQL.

## Testing the Setup

### Test 1: Verify Database Connection

Once the app is running, you can test the endpoints. If using the default port (check your terminal output), the base URL is typically `http://localhost:5000` or `https://localhost:5001`.

#### Create a Branch

```powershell
curl -X POST http://localhost:5000/api/Branches `
  -H "Content-Type: application/json" `
  -d '{\"name\":\"Main Branch\",\"address\":\"123 Main St\"}'
```

#### Get All Branches

```powershell
curl http://localhost:5000/api/Branches
```

#### Create a Customer

```powershell
curl -X POST http://localhost:5000/api/Customers `
  -H "Content-Type: application/json" `
  -d '{\"firstName\":\"John\",\"lastName\":\"Doe\",\"email\":\"john@example.com\"}'
```

#### Get All Customers

```powershell
curl http://localhost:5000/api/Customers
```

### Test 2: Verify PostgreSQL Directly

Connect to the PostgreSQL container:

```powershell
docker exec -it bankingapi-postgres psql -U postgres -d bankingdb
```

Once connected, run SQL queries:

```sql
-- List all tables
\dt

-- View Branches
SELECT * FROM "Branches";

-- View Customers
SELECT * FROM "Customers";

-- View Accounts
SELECT * FROM "Accounts";

-- Exit
\q
```

### Test 3: Check Migration History

```sql
-- In PostgreSQL, check applied migrations
SELECT * FROM "__EFMigrationsHistory";
```

## Common EF Core Migration Commands

### Create a new migration
```powershell
dotnet ef migrations add <MigrationName>
```

### List all migrations
```powershell
dotnet ef migrations list
```

### Update database to latest migration
```powershell
dotnet ef database update
```

### Update database to specific migration
```powershell
dotnet ef database update <MigrationName>
```

### Remove last migration (if not applied)
```powershell
dotnet ef migrations remove
```

### Generate SQL script for migration
```powershell
dotnet ef migrations script
```

### Drop database
```powershell
dotnet ef database drop
```

## Managing Docker Compose

### Stop the database (data preserved)
```powershell
docker compose down
```

### Stop and remove all data
```powershell
docker compose down -v
```

### View container logs
```powershell
docker compose logs -f db
```

### Restart the database
```powershell
docker compose restart
```

## Connection String Details

The connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5433;Database=bankingdb;Username=postgres;Password=postgres"
}
```

**Note:** Port 5433 is used on the host machine to avoid conflicts with any local PostgreSQL installation (which typically uses 5432).

## Troubleshooting

### Port Already in Use

If port 5433 is already in use, edit `docker-compose.yml` and change the port mapping:

```yaml
ports:
  - "5434:5432"  # Change 5433 to 5434 or another available port
```

Then update the connection string in `appsettings.json` accordingly.

### Container Won't Start

Check Docker Desktop is running and you have sufficient resources allocated.

### Connection Refused

Ensure the container is healthy:

```powershell
docker compose ps
```

Wait a few seconds for the health check to pass.

### Migration Errors

If you encounter migration errors, try:

1. Drop the database: `dotnet ef database drop`
2. Delete the Migrations folder
3. Recreate the migration: `dotnet ef migrations add InitialCreate`
4. Apply it: `dotnet ef database update`

## Additional Resources

- [PostgreSQL Docker Hub](https://hub.docker.com/_/postgres)
- [Entity Framework Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Npgsql Entity Framework Core Provider](https://www.npgsql.org/efcore/)

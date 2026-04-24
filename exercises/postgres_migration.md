# Exercise — Migrate `BankingApi` from in-memory to Postgres

**Reference lessons (in order):**
- [`../lessons/postgres/1_lesson_swap_provider.md`](../lessons/postgres/1_lesson_swap_provider.md)
- [`../lessons/postgres/2_lesson_first_migration.md`](../lessons/postgres/2_lesson_first_migration.md)
- [`../lessons/postgres/3_lesson_schema_changes_and_rollbacks.md`](../lessons/postgres/3_lesson_schema_changes_and_rollbacks.md)

By the end: your `BankingApi` project talks to a real Postgres database, its schema is managed by EF migrations, and the data you create survives a restart. Optionally (stretch), you've evolved the schema once and rolled back a migration.

**Prerequisites:**
- You've completed Exercise 6 — `BankingApi` has `Account` + `Transaction` with the FK, the scaffolded controllers, `.Include(a => a.Transactions)`, and `[JsonIgnore]` on `Transaction.Account`.
- You can run either Docker (for the bundled Postgres stack) or an existing native Postgres on your machine.
- `dotnet-ef` installed globally: `dotnet tool install --global dotnet-ef` (or `update` if you already have it). The version must match your project's EF Core version — run `dotnet ef --version` and compare against the `Microsoft.EntityFrameworkCore.*` package version in `BankingApi.csproj`.

---

## Core

### 1. Swap the EF Core provider

From inside `BankingApi/`:

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet remove package Microsoft.EntityFrameworkCore.InMemory
```

### 2. Get a Postgres connection string

Two paths — pick one:

**Path A — I already have Postgres.** Use your own credentials; plug them into step 3 below. Create a `bank` database first (`createdb -U postgres bank` or via pgAdmin).

**Path B — I need a local Postgres.** From the repo root:

```bash
cd lessons/postgres/infra
docker compose up -d
docker compose ps   # confirm service "db" is running
```

The compose file publishes Postgres on host port `5433` (container `5432`) with user `postgres` / password `postgres` / database `bank`.

### 3. Wire up the connection string

Add a `ConnectionStrings` section to `appsettings.Development.json`:

```json
{
  "Logging": { ... },
  "ConnectionStrings": {
    "AppDb": "Host=localhost;Port=5433;Database=bank;Username=postgres;Password=postgres"
  }
}
```

If your Postgres runs on different credentials (Path A), adjust this line to match — nothing else in the exercise depends on these exact values.

### 4. Swap `UseInMemoryDatabase` for `UseNpgsql`

In `Program.cs`, replace the existing DbContext registration:

```csharp
var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Connection string 'AppDb' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

The throw-on-missing is intentional: a missing connection string is a config bug, not a recoverable runtime error.

### 5. Generate the initial migration

```bash
dotnet ef migrations add InitialCreate
```

> **Visual Studio equivalent:** `Add-Migration InitialCreate` in the Package Manager Console. Make sure the **Default project** dropdown points at `BankingApi`.

This creates a `Migrations/` folder with three files. Open `Migrations/<timestamp>_InitialCreate.cs` and read it. You should see:

- `CreateTable("Accounts", ...)` with your columns.
- `CreateTable("Transactions", ...)` with the `TodoListId`-style foreign key.
- A corresponding `Down` that drops the tables.

If you also completed the L1 stretches (Branch / Customer), their tables appear here too.

### 6. Replace `EnsureCreated` with `Migrate`

Before you apply the migration, update the startup scope block in `Program.cs`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();        // was: db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}
```

`EnsureCreated` and `Migrate` must not coexist — `EnsureCreated` would create tables outside the migration history, and `Migrate` would then fail trying to create them again.

### 7. Apply the migration

Restart the app (`dotnet run`). `Migrate()` runs `InitialCreate.Up` against your Postgres and records it in `__EFMigrationsHistory`. Your `DbSeeder.Seed(db)` call then populates the three seeded accounts as before.

If the app starts cleanly, you're done with core. The seeded data lives in Postgres now.

### Acceptance check

```bash
# 1) GET the seeded accounts
curl http://localhost:<port>/api/Accounts
# → three accounts: ACC-1000, ACC-1001, ACC-1002

# 2) Create a new one
curl -X POST http://localhost:<port>/api/Accounts \
     -H "Content-Type: application/json" \
     -d '{"accountNumber":"ACC-PERSIST"}'
# → 201 Created with the new account

# 3) STOP the app (Ctrl+C), then `dotnet run` again.
# 4) GET the list again
curl http://localhost:<port>/api/Accounts
# → FOUR accounts, including "ACC-PERSIST" from step 2
```

**The fourth account surviving the restart is the whole point.** In-memory lost your data every run; Postgres doesn't.

Verify the migration is tracked:

```bash
psql -h localhost -p 5433 -U postgres -d bank -c 'SELECT * FROM "__EFMigrationsHistory";'
# → one row with MigrationId matching your InitialCreate timestamp
```

---

## Stretch

### S1 — Evolve the schema

Add a `Currency` column to `Account`:

```csharp
// Models/Account.cs
public string Currency { get; set; } = "GBP";
```

Generate the migration:

```bash
dotnet ef migrations add AddAccountCurrency
```

Open the generated file and read it **before applying.** You should see:

- `AddColumn<string>(name: "Currency", table: "Accounts", ... defaultValue: "GBP")` — EF backfilled your default onto existing rows.
- `DropColumn` in `Down`.

Apply it:

```bash
dotnet ef database update
```

#### Acceptance

```bash
psql -h localhost -p 5433 -U postgres -d bank -c '\d "Accounts"'
# → table description includes: "Currency" text NOT NULL DEFAULT 'GBP'::text

curl http://localhost:<port>/api/Accounts
# → every account response now has "currency": "GBP"
```

### S2 — Roll back the schema change

Scenario: you realise `Currency` should have been an enum, not a free-form string. Roll back cleanly.

```bash
dotnet ef database update InitialCreate
```

> **Visual Studio equivalent:** `Update-Database InitialCreate`

This runs `AddAccountCurrency.Down` — dropping the column — and removes its row from `__EFMigrationsHistory`. Your app's C# code still references `Account.Currency`, so it will crash on the next query. That's expected.

Now remove the migration files and the model property cleanly:

1. Remove the `Currency` property from `Account.cs`.
2. `dotnet ef migrations remove` (this deletes the generated files AND rewinds `AppDbContextModelSnapshot.cs`).

> **Never delete migration files by hand** — the snapshot won't be updated and the next `migrations add` will produce nonsense.

#### Acceptance

```bash
dotnet ef migrations list
# → only InitialCreate

psql -h localhost -p 5433 -U postgres -d bank -c '\d "Accounts"'
# → no "Currency" column

curl http://localhost:<port>/api/Accounts
# → works again, responses have no "currency" field
```

---

## Done?

Core passing: your project persists data across restarts, and the schema is tracked by migrations instead of recreated by `EnsureCreated`.
Stretch passing: you've evolved a schema and rolled back an applied migration without losing the migration tooling's consistency.

From here, `dotnet ef migrations add <Name>` is how every schema change reaches the database. The in-memory provider served its purpose — you won't miss it.

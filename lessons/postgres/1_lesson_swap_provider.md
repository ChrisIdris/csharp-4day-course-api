# Postgres Lesson 1 — Swap the in-memory provider for Postgres

Every lesson so far has used EF Core's **in-memory** provider. It's been perfect for learning: no install, no connection string, no moving parts. It's also a dead end for anything beyond learning.

- Every `dotnet run` starts you with a fresh empty database. Nothing you wrote yesterday survives.
- No indexes, no constraints beyond what EF infers in memory, no real type checking.
- No story for two processes talking to the same data.
- No migration history — the "schema" is just whatever your C# classes look like right now.

This lesson starts the real-database track. You'll swap the in-memory provider for **PostgreSQL**, wire up a connection string, and get the same API running against a database that actually survives a restart. Nothing else changes — no migrations yet (that's Lesson 2), no schema evolution yet (Lesson 3). Just the swap.

Start from your Lesson 5 project (or open `lessons/Lesson5` in this repo and copy it forward).

---

## 1. Get a Postgres connection string

The only thing your app needs is a **connection string** pointing at a running Postgres. Where that Postgres lives is an infrastructure decision — native install, Docker container, remote dev server, managed cloud DB — the app doesn't care as long as it can reach the host and port.

Pick the path that matches your setup:

### Path A — I already have Postgres installed

Use whatever you already have. The default connection string this course expects is:

```
Host=localhost;Port=5433;Database=bank;Username=postgres;Password=postgres
```

If your instance runs on a different port, uses different credentials, or you want a different database name, **edit the value in `appsettings.Development.json` in step 3** below to match what your Postgres is actually configured with. No other step in any lesson needs to change.

Create the `bank` database (or a name of your choice, adjusted in the connection string):

```bash
createdb -U postgres bank
```

…or use pgAdmin / Azure Data Studio.

### Path B — I need a local Postgres

This track ships a `docker-compose.yml` under `lessons/postgres/infra/` that stands one up for you. From the repo root:

```bash
cd lessons/postgres/infra
docker compose up -d
docker compose ps   # confirm service "db" is running
```

Credentials and database name in the compose file match the default connection string above. Note the **port mapping: host `5433` → container `5432`**. The container's Postgres listens on the canonical `5432` internally; your host sees it on `5433`. This is deliberate — it avoids colliding with any native Postgres you or a teammate may already run on `5432`.

Stop the stack later with `docker compose down` (data survives in the named volume); `docker compose down -v` wipes the volume too for a fresh start.

Read the comments inside `infra/docker-compose.yml` if you want the full rundown of commands.

---

## 2. Install the Npgsql provider

From inside your `Lesson5/` (or whichever project you're evolving):

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

> **In Visual Studio:** Right-click the project → **Manage NuGet Packages…** → **Browse** → search `Npgsql.EntityFrameworkCore.PostgreSQL` → **Install**.

You can also remove `Microsoft.EntityFrameworkCore.InMemory` — you won't need it any more:

```bash
dotnet remove package Microsoft.EntityFrameworkCore.InMemory
```

…but leaving it installed does no harm, and keeping it lets you A/B switch during debugging if you want. Your call.

---

## 3. Wire up the connection string

Your `Program.cs` currently has something like:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));
```

Change it to read a named connection string from configuration and hand it to `UseNpgsql`:

```csharp
var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Connection string 'AppDb' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

Two things worth noticing:

- **`builder.Configuration.GetConnectionString("AppDb")`** reads from any of the configuration sources ASP.NET Core loads by default: `appsettings.json`, `appsettings.{Environment}.json`, environment variables, command-line args, user-secrets. The app doesn't care *where* the value came from — it just asks for the string named `"AppDb"`.
- **Throw if missing.** A missing connection string is a configuration bug, not a recoverable runtime error. Fail at startup so the mistake is obvious, not silent.

### Where to put the value

Three places, in increasing order of secrecy:

**Dev — `appsettings.Development.json`** (committed to the repo, shared across the team):

```json
{
  "ConnectionStrings": {
    "AppDb": "Host=localhost;Port=5433;Database=bank;Username=postgres;Password=postgres"
  }
}
```

This is fine for local dev credentials — they're not secrets, and they match whatever Postgres you stood up in step 1 (native install or the `infra/docker-compose.yml` stack). If your local Postgres uses different credentials or a different port, edit this line to match; nothing else in any lesson depends on these exact values.

**Real secrets (later) — environment variable:**

```bash
export ConnectionStrings__AppDb="Host=prod-db.internal;Port=5432;Database=bank;Username=app;Password=SuperSecret"
```

The double underscore `__` is ASP.NET Core's convention for nesting — it maps to `ConnectionStrings:AppDb` in the config tree. Environment variables override anything in `appsettings.json`, which is why you never commit prod values.

**Or user-secrets (dev, but personal):**

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:AppDb" "Host=localhost;..."
```

Stored outside the repo, per-machine. Useful when your dev DB credentials aren't the team defaults.

**Pick one.** For this course, go with **`appsettings.Development.json`** — the local credentials above are not secrets and can be committed. When the time comes to deploy, environment variables are the production answer. User-secrets is the "I want to deviate from the team default" escape hatch.

> **In Visual Studio:** Right-click the project → **Manage User Secrets** opens `secrets.json` ready to edit — equivalent to the `dotnet user-secrets` commands above.

---

## 4. Let EF create the schema (temporarily)

You have no migrations yet — they're Lesson 2. So the DB starts empty, and EF has no idea what schema to create. For this lesson only, let EF auto-create the tables from your model via `EnsureCreated` at startup. It's the same call you've been using in `DbSeeder`-adjacent code since Lesson 3:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // TEMPORARY — Lesson 2 replaces this with db.Database.Migrate()
    DbSeeder.Seed(db);
}
```

If you've been following the course, this block already exists — `EnsureCreated` was harmless against the in-memory provider. Against Postgres it actually creates tables. It also **won't** ever modify an existing schema — it creates tables only if the DB is empty. That's why it's a dead end for anything past the first run, and why Lesson 2 replaces it with real migrations.

---

## Acceptance check

Make sure your Postgres is running — `docker compose ps` (if you took Path B) or the equivalent health check for your native install.

```bash
dotnet run
```

You should see startup log lines from EF including SQL like `CREATE TABLE "TodoLists" (...)`. The app binds to its usual port.

Then:

```bash
# 1) The seeded data is there
curl http://localhost:<port>/api/TodoLists
# → array with "Inbox" + the DbSeeder's demo lists

# 2) Create something
curl -X POST http://localhost:<port>/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Persists across restart"}'
# → 201 Created with the new list

# 3) Stop the app (Ctrl+C), start again (dotnet run), fetch:
curl http://localhost:<port>/api/TodoLists
# → The new list IS STILL THERE
```

**That last step is the whole point.** In-memory lost your data on every restart. Postgres doesn't.

### Inspect the database

Install **Azure Data Studio** with the Postgres extension (free, cross-platform) or open **pgAdmin** if you already have it. Connect with the credentials from your connection string (by default: host `localhost`, port `5433`, user `postgres`, password `postgres`, database `bank`). You should see your tables and their contents.

> **`psql` CLI alternative.** If you like terminals: `psql -h localhost -p 5433 -U postgres -d bank -c "\dt"` lists tables; `\d+ "TodoLists"` shows the schema; `SELECT * FROM "TodoLists";` shows the rows. Note the double quotes — Postgres folds unquoted identifiers to lowercase, and EF's PascalCase names need quoting to come through verbatim.

---

## Where this leaves you

Your API is backed by a real database. Data persists. You can inspect the schema in a GUI. You have a dev/prod configuration story.

But `EnsureCreated()` is a band-aid. The second you change a model field, EF has no way to evolve the existing schema — it won't touch an existing database at all. That's what **migrations** are for.

**Next — Lesson 2.** Install `dotnet-ef`, generate your first migration, apply it, and see how seed data flows through the migration machinery. By the end of Lesson 2 you'll have deleted the `EnsureCreated` line and replaced it with `Database.Migrate()`.

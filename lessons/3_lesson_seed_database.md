# Lesson 3 — Seeding the in-memory database (two ways)

The in-memory EF Core provider lives inside your app's process. Every `dotnet run` starts it empty, which makes demos feel anaemic — you open Scalar, hit `GET /api/TodoLists`, and get `[]`. We'll fix that by **seeding** the database with baseline data at startup.

EF Core gives us two different tools for this, and they're not interchangeable — each is better at a different job. We'll use both:

1. **`HasData` in `OnModelCreating`** — seed an **Inbox** list that every instance of the app must have and that cannot be deleted.
2. **A `DbSeeder` class** — add dev-only demo rows so our Scalar UI isn't empty when the team plays with the API.

Start from a copy of your Lesson 2 project (or open `lessons/Lesson3` in this repo).

Before we seed, update the model to add `Deletable` (copy it from Lesson 1 if you skipped it) — we need that flag for the Inbox row.

---

## Approach 1 — `HasData` in `OnModelCreating` (for reference data)

Open `Data/AppDbContext.cs` and override `OnModelCreating`:

```csharp
using Lesson3.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson3.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TodoList>().HasData(
            new TodoList
            {
                Id = 1,
                Title = "Inbox",
                Description = "Your default list — cannot be deleted.",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Deletable = false
            }
        );
    }
}
```

### How it works

`OnModelCreating` is where EF Core learns the **shape** of your database: keys, relationships, indexes, and — via `HasData` — baseline rows. It runs once, when EF builds the model at app startup. Those seed rows are then inserted when the database is created (`EnsureCreated()`) or when migrations are applied (`Database.Migrate()`).

Because it runs before any connection exists, `HasData` has strict limits:

- You **must** supply the primary key (`Id = 1`). EF can't hand out auto-increment values at model-build time.
- Every value must be a **compile-time constant**. You can't call `DateTime.UtcNow`, read a file, or resolve a service — that's why the Inbox's `CreatedAt` is a fixed `2026-01-01`.

### When to use it

`HasData` is the right tool for **reference data that's part of the schema**:

- A user's **Inbox** list that must always exist and must not be deletable.
- A fixed set of `Role` rows — "admin", "editor", "viewer".
- Lookup tables of currencies, country codes, statuses.
- Any row the app's logic relies on at every startup.

In a production app using real migrations, changing a `HasData` row produces a migration, so seed changes are **versioned alongside your schema**. That's exactly what you want for reference data — it's data the app depends on to work at all.

### When it's wrong

Use a different approach whenever you need:

- **Runtime values** — `DateTime.UtcNow`, a hashed password, a value from `IConfiguration`.
- **Async work** — reading a JSON fixture file, calling an HTTP API for sample content.
- **Volume** — dozens of demo rows that aren't semantically part of the schema.

---

## Approach 2 — A `DbSeeder` class (for nuanced / dev data)

Create `Data/DbSeeder.cs`:

```csharp
using Lesson3.Models;

namespace Lesson3.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        // Idempotency: if our demo rows are already in the DB, bail out. We can't just
        // check "any rows exist" because HasData has already inserted the Inbox. Picking
        // a title that's unique to our demo data is a simple sentinel.
        if (db.TodoLists.Any(x => x.Title == "Groceries"))
        {
            return;
        }

        db.TodoLists.AddRange(
            new TodoList { Title = "Groceries",   Description = "Weekly shop" },
            new TodoList { Title = "Work tasks",  Description = "Sprint 42 backlog" },
            new TodoList { Title = "House chores" }
        );

        db.SaveChanges();
    }
}
```

A seeder class is just a plain C# method. It has **all of C# available**: you can compute timestamps, hash passwords, read files, call services via DI, do async work, branch on environment. It runs every startup, so the `if (Any(...)) return;` check keeps it from duplicating data.

### Call it from `Program.cs`

Add a new block after `var app = builder.Build();` and before the `if (app.Environment.IsDevelopment())` block:

```csharp
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // <- this is when HasData actually inserts the Inbox
    DbSeeder.Seed(db);             // <- then our dev rows
}

if (app.Environment.IsDevelopment()) { /* ...MapOpenApi, MapScalarApiReference... */ }
```

This block is the trickiest code so far. Let's unpack it.

### Why `CreateScope()`?

When we registered `AppDbContext` with `AddDbContext<T>` back in Lesson 1, ASP.NET gave it **scoped** lifetime — one instance per HTTP request. That's what you want in controllers: each request gets a fresh context with its own change tracker.

But here we're running **before any request exists**. The app is still starting up. `app.Services` is the **root** (singleton) service provider, and it deliberately refuses to hand out scoped services — that would leak them forever.

The workaround is to **create a scope manually**:

```csharp
using (var scope = app.Services.CreateScope())
{
    // inside this block, scope.ServiceProvider CAN hand out scoped services
}
```

The `using` ensures the scope (and therefore our `DbContext`) is disposed as soon as seeding finishes.

### Why `EnsureCreated()`?

For the **in-memory provider** it's nearly a no-op. But more importantly, it's the step that actually **inserts the `HasData` rows** into the database. Skip this call and your Inbox never shows up.

With a real provider (SQL Server, SQLite, Postgres) `EnsureCreated()` also creates the schema. In a production app you'd usually call `db.Database.Migrate()` instead — that applies migrations — but the principle is the same: the seed rows from `HasData` are inserted as part of schema setup.

---

## Run it

```bash
dotnet run
```

Check Scalar (`http://localhost:5107/scalar/v1`) or curl:

```bash
curl http://localhost:5107/api/TodoLists
```

You should see **4 rows**:

```json
[
  { "id": 1, "title": "Inbox",        "deletable": false, ... },
  { "id": 2, "title": "Groceries",    "deletable": true,  ... },
  { "id": 3, "title": "Work tasks",   "deletable": true,  ... },
  { "id": 4, "title": "House chores", "deletable": true,  ... }
]
```

Try to delete the Inbox:

```bash
curl -i -X DELETE http://localhost:5107/api/TodoLists/1
```

```
HTTP/1.1 400 Bad Request
{"type":"...","title":"Bad Request","status":400,
 "detail":"This list is marked as non-deletable and cannot be removed.",...}
```

The `Deletable` guard we built in Lesson 1 is now exercised for real. Delete `Groceries` (id 2) and it succeeds with `204 No Content`.

---

## Side-by-side: which tool, when

| Kind of data | Best fit | Why |
|---|---|---|
| **Reference / lookup data** (Inbox, roles, currencies) | `HasData` | Belongs in the schema; versioned with migrations; idempotency is free |
| **Dev/demo fixtures** | `DbSeeder` class | Runtime values, volume, freely editable without a migration |
| Data that needs **`DateTime.UtcNow`, hashed values, async work, DI services** | `DbSeeder` class | `HasData` is compile-time only |
| Production **baseline rows** required by app logic | `HasData` | Guarantees every environment has them, visible in migration history |

### Pros / cons cheat sheet

**`HasData`**

- ✅ Idempotent for free (EF uses the `Id` as the key; no duplicates on restart).
- ✅ Seed data is **part of the model** → migrations track changes.
- ✅ No scope/DI juggling in `Program.cs`.
- ❌ Values must be compile-time constants (no `DateTime.UtcNow`, no service calls).
- ❌ You must supply `Id` values yourself, and changing one later is a migration.
- ❌ Adds concepts — migrations, model-level data — on top of basic CRUD.

**`DbSeeder` class**

- ✅ Full C# — async, DI, `DateTime.UtcNow`, reading files.
- ✅ Easy to iterate on demo data without touching migrations.
- ✅ Identical behaviour across providers (InMemory / SQLite / SQL Server / Postgres).
- ✅ Teaches **scoped vs singleton** service lifetimes (`CreateScope()`).
- ❌ Idempotency is your problem (the title sentinel check, here).
- ❌ Not tracked as schema — two environments can diverge if you forget to run it.

---

## Recap

- **`HasData` = schema**. The Inbox is a part of what "a working app" means; it's seeded at model-build time and versioned with migrations.
- **`DbSeeder` = runtime**. Dev data, dynamic values, async work — everything `HasData` can't do.
- Use both together: `HasData` for the Inbox; `DbSeeder` for the demo rows that make your UI look alive.

In **Lesson 4** we'll tackle an automated cross-cutting concern: keeping an `UpdatedAt` timestamp fresh on every write — by overriding `SaveChanges` on the DbContext.

# Postgres Lesson 2 — Your first migration

Lesson 1 left you in an uncomfortable spot. Your app runs against Postgres, but the schema is built by `EnsureCreated()` — a one-shot call that creates tables if the DB is empty and does **nothing** if it isn't. Change a model, restart the app, and your existing database is silently out of date: the code expects a column the DB doesn't have, and you'll crash on the next query.

**Migrations** fix this. A migration is a pair of code-generated operations — `Up` (apply the change) and `Down` (undo it) — that turn one schema version into the next. You add migrations as your model evolves, and EF keeps a `__EFMigrationsHistory` table in Postgres recording which ones have been applied. A fresh database catches up by running every migration in order; a live database catches up by running only the ones it hasn't seen.

By the end of this lesson: you have an `InitialCreate` migration applied to Postgres, you've removed the `EnsureCreated()` band-aid, and you understand what `HasData` produces when it flows through a migration.

Start from your Lesson 1 state (the project from `lessons/postgres/1_lesson_swap_provider.md`).

---

## 1. Install the `dotnet-ef` CLI tool

Once per machine:

```bash
dotnet tool install --global dotnet-ef
```

If you've installed an older version before:

```bash
dotnet tool update --global dotnet-ef
```

Verify:

```bash
dotnet ef --version
# → some 9.x.x line — anything matching your EF Core version is fine
```

> **In Visual Studio:** You don't need to install anything separately. The **Package Manager Console** (Tools → NuGet Package Manager → Package Manager Console) has equivalent commands baked in via the `Microsoft.EntityFrameworkCore.Tools` package — which is already referenced by your project (it's one of the packages from Lesson 1 step 3 of the main track).

---

## 2. Add the first migration

From the project folder:

```bash
dotnet ef migrations add InitialCreate
```

> **Visual Studio PMC equivalent:** `Add-Migration InitialCreate`
>
> Make sure the **Default project** dropdown at the top of the PMC window points at the correct project — that's the one Visual Studio gotcha for EF Core commands.

EF inspects your `AppDbContext`, diffs it against the current migration state (none yet), and generates two files under a new `Migrations/` folder:

```
Migrations/
├── 20260424_InitialCreate.cs          ← the Up / Down migration code
├── 20260424_InitialCreate.Designer.cs ← snapshot metadata — don't edit
└── AppDbContextModelSnapshot.cs       ← rolling snapshot of the model — don't edit
```

Open `20260424_InitialCreate.cs`. You'll see two methods: `Up` (what to do to apply this migration) and `Down` (how to undo it). Each one is a sequence of `migrationBuilder.CreateTable(...)` / `DropTable(...)` calls that describe the schema changes in provider-agnostic form — EF translates them to Postgres SQL at apply time.

Two things worth noticing:

- **`Up` creates, `Down` destroys.** If you ever want to roll back this migration, `Down` is what runs. Lesson 3 uses this.
- **`HasData` becomes `InsertData` calls.** Scroll to the end of `Up`. Every row you seeded via `modelBuilder.Entity<TodoList>().HasData(...)` in your `OnModelCreating` (Lesson 4 of the main track) appears here as an explicit `migrationBuilder.InsertData(...)` call. That's the seed-data-in-migrations story — seeded rows are part of the schema version, not of the runtime seeder.

---

## 3. Apply the migration

```bash
dotnet ef database update
```

> **Visual Studio PMC equivalent:** `Update-Database`

EF connects to Postgres using the same connection string your app uses, reads the `__EFMigrationsHistory` table (creates it if missing), sees that no migrations have been applied, and runs `InitialCreate.Up` against your database.

Verify with Azure Data Studio, pgAdmin, or `psql`:

```bash
psql -h localhost -p 5433 -U postgres -d bank -c '\dt'
# → a list of tables including "TodoLists", "Todos", and "__EFMigrationsHistory"

psql -h localhost -p 5433 -U postgres -d bank -c 'SELECT * FROM "__EFMigrationsHistory";'
# → one row: MigrationId = "20260424_InitialCreate", ProductVersion = "9.x.x"
```

Your seeded rows are also there:

```bash
psql -h localhost -p 5433 -U postgres -d bank -c 'SELECT "Id", "Title" FROM "TodoLists";'
# → the "Inbox" row and any other HasData rows
```

---

## 4. Remove the `EnsureCreated` band-aid

Open `Program.cs`. The startup block from Lesson 1 looks like:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // TEMPORARY — Lesson 2 replaces this
    DbSeeder.Seed(db);
}
```

Replace `EnsureCreated()` with `Migrate()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();        // apply any pending migrations at startup
    DbSeeder.Seed(db);
}
```

**Important distinction:**

- **`EnsureCreated()`** — "if no DB exists, create one from the current model." Ignores migrations entirely. Once a DB exists, this is a no-op.
- **`Migrate()`** — "apply every migration that hasn't been applied yet." Uses `__EFMigrationsHistory` to know which. Works on a fresh DB (runs all migrations in order) AND on a live DB (runs only the new ones).

Using both is a **bug**: `EnsureCreated` creates tables outside the migration history, then `Migrate` tries to create them again and fails. Delete `EnsureCreated`.

Restart the app and confirm it still works:

```bash
dotnet run
curl http://localhost:<port>/api/TodoLists
# → the seeded data — same as before
```

---

## 5. The seed-data upsert question

Here's the scenario that trips everyone up at least once. You change the `Description` of the `Inbox` seed:

```csharp
modelBuilder.Entity<TodoList>().HasData(
    new TodoList
    {
        Id = 1,
        Title = "Inbox",
        Description = "Your default list — now with a different description.",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Deletable = false
    }
);
```

**You might think** that on next `dotnet run`, EF will see the change and update the row. **It won't.** The running app doesn't re-read `HasData` against the live DB; `HasData` only means anything at *migration-generation time*.

To propagate the change, generate a new migration:

```bash
dotnet ef migrations add UpdateInboxDescription
```

Open the new migration. You'll see a `migrationBuilder.UpdateData(...)` call targeting `(table: "TodoLists", keyColumn: "Id", keyValue: 1)` with the new `Description` column value. Apply it:

```bash
dotnet ef database update
```

Now the Postgres row matches the new seed value. Had you **deleted** an entry from `HasData` instead, the generated migration would be a `DeleteData` call. Had you **added** one, it would be `InsertData`. EF computes the diff against the snapshot and emits the minimum SQL to reach the new state — an upsert, give or take.

**The rule to remember:** every change to seeded data needs its own migration. There is no "re-seed at startup" that competes with the migration history.

---

## Acceptance check

You pass this lesson when all four hold:

1. `dotnet ef migrations list` shows `InitialCreate` (and possibly `UpdateInboxDescription` if you did step 5).
2. The `__EFMigrationsHistory` table in Postgres has a row for every applied migration.
3. Your `Program.cs` calls `db.Database.Migrate()` and **does not** call `db.Database.EnsureCreated()`.
4. `dotnet run` → `curl http://localhost:<port>/api/TodoLists` returns the seed data, and the data survives a restart.

---

## Where this leaves you

The schema is managed by migrations. Seed data rides in those migrations as explicit `InsertData` / `UpdateData` / `DeleteData` calls. Every schema version is one `dotnet ef database update` away.

But we haven't actually evolved the schema yet — `InitialCreate` just serialised what already existed. **Lesson 3** covers the real thing: adding a field to a model, generating a second migration, applying it, rolling it back when you realise you got it wrong, and the production-deployment story (script it vs run `Migrate` on startup).

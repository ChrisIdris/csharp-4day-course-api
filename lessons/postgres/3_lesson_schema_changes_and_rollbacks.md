# Postgres Lesson 3 — Evolving the schema, and how to undo it

Lesson 2 gave you an `InitialCreate` migration that just captured the schema you already had. That's the easy case. The real value of migrations shows when you **change** the schema — add a field, drop a column, rename a table — and need the database to catch up without losing the data it already holds.

This lesson covers the three operations you'll do every day from now on:

- **Add** a migration for a schema change.
- **Roll back** a migration you've already applied.
- **Discard** a migration that hasn't been applied yet, because you realised it's wrong.

It closes with the production-deployment question: how do these migrations actually reach a live database when you ship?

Start from your Lesson 2 state — `InitialCreate` applied, `Migrate()` in `Program.cs`, no `EnsureCreated()`.

---

## 1. Add a field to a model

Add a `Priority` integer to `Todo`:

```csharp
// Models/Todo.cs
public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public int Priority { get; set; }          // ← NEW — 0 = default
    // ...rest unchanged...
}
```

A plain `int` — not `int?` — so every existing row needs a default value. We'll see EF handle that automatically in the next step.

---

## 2. Generate the migration

```bash
dotnet ef migrations add AddTodoPriority
```

> **Visual Studio PMC equivalent:** `Add-Migration AddTodoPriority`

Open `Migrations/<timestamp>_AddTodoPriority.cs`. You'll see a short `Up`:

```csharp
migrationBuilder.AddColumn<int>(
    name: "Priority",
    table: "Todos",
    type: "integer",
    nullable: false,
    defaultValue: 0);
```

and a corresponding `Down`:

```csharp
migrationBuilder.DropColumn(name: "Priority", table: "Todos");
```

Two things to notice:

- **`defaultValue: 0`.** Because we used a non-nullable `int`, Postgres needs to backfill existing `Todos` rows with *some* value. EF picked the type's default. Had we used `int? Priority`, the migration would have created a nullable column and existing rows would have `NULL`.
- **`Down` is the exact inverse.** EF drops the column. If the column had live data you wanted to preserve, `Down` would silently delete it — that's a real thing to think about before rolling back a production database. On a dev database, you don't care.

### Read the generated file *before* applying it

This is the single most important habit of migration-based development. EF's diff is usually right, but "usually" isn't "always". A renamed property, for example, looks to EF like "drop the old column, add a new one" — which destroys data. Catching that in the generated file is cheap; catching it after `database update` has run in production is not.

If this migration looks wrong, **don't apply it**. Remove it (step 5) and try again.

---

## 3. Apply the migration

Assuming the generated file looks right:

```bash
dotnet ef database update
```

> **Visual Studio PMC equivalent:** `Update-Database`

EF runs the migration's `Up` against Postgres, then appends a row to `__EFMigrationsHistory`. Verify:

```bash
psql -h localhost -p 5433 -U postgres -d bank -c '\d "Todos"'
# → a table description including "Priority | integer | not null | default 0"

psql -h localhost -p 5433 -U postgres -d bank -c 'SELECT "Id", "Title", "Priority" FROM "Todos";'
# → existing rows now have Priority = 0
```

Your seed data survived; the new column is populated with zeros; the app still runs.

---

## 4. Roll back — the applied-migration case

You shipped `AddTodoPriority`. A week later you realise `Priority` should have been a string enum, not an int. You want to undo this migration cleanly so you can design a replacement.

The command is `database update <TargetMigration>` — which means "apply or roll back until the database is at exactly that migration":

```bash
dotnet ef database update InitialCreate
```

> **Visual Studio PMC equivalent:** `Update-Database InitialCreate`

EF sees `AddTodoPriority` is applied and the target is `InitialCreate`, so it runs `AddTodoPriority.Down` (dropping the `Priority` column) and removes the row from `__EFMigrationsHistory`.

**Your C# code still references `Priority`.** The app will crash on the next query against `Todos` because the column no longer exists. That's expected — rolling back the database without also reverting the C# is half an operation. Remove the `Priority` property from `Todo.cs`, delete the `AddTodoPriority` migration file (step 5), and you're back to a clean `InitialCreate`-only state.

> **Rollback on real data — a warning.** `Down` destroys the column and its values. If `Priority` had been populated with user data you cared about, you'd have lost it. Production rollback is sometimes "roll forward with a corrective migration" instead — write a `RevertPriority` migration that un-does the schema change while preserving anything salvageable. For now, on dev data, the direct rollback is fine.

---

## 5. Discard — the not-yet-applied case

Different scenario: you ran `dotnet ef migrations add AddTodoPriority`, read the generated file, and realised the change is wrong. The migration is on disk but **not applied** yet (no `dotnet ef database update` has run).

Do **not** delete the file by hand. EF keeps a snapshot of the "current" model in `Migrations/AppDbContextModelSnapshot.cs` that's updated whenever you add a migration; deleting the migration file leaves the snapshot inconsistent, and the next `migrations add` will produce nonsense.

Use the tool:

```bash
dotnet ef migrations remove
```

> **Visual Studio PMC equivalent:** `Remove-Migration`

This deletes the migration files **and** rewinds the snapshot. You're back to the state you were in before the `add` command.

**`migrations remove` refuses to run if the migration has already been applied** to the database. That's a safety rail. If you applied it and want it gone, use step 4 first to roll back, *then* `migrations remove`.

### The practical "dry-run" workflow

EF doesn't have a `--dry-run` flag. It doesn't need one — this is the workflow:

1. `dotnet ef migrations add CandidateChange`
2. Read the generated `Up` and `Down`.
3. If it's right → `dotnet ef database update`.
4. If it's wrong → `dotnet ef migrations remove`, fix the model, go back to step 1.

You're generating the migration as a *proposal*, reading it, and either accepting or rejecting it. That's the dry-run.

---

## 6. The production story

Two patterns. Both are legitimate; pick based on how your team ships.

### Pattern A — `Migrate()` on startup

What you have right now. `Program.cs` calls `db.Database.Migrate()`, so every deploy applies any pending migrations when the app starts.

**Pros:** Zero operational overhead. Deploy the code; schema updates itself. Fine for small teams and solo projects.

**Cons:** The running app has the DB permissions required to alter schema (`CREATE TABLE`, `ALTER TABLE`, etc.) — a wider privilege surface than the app needs during normal operation. If the migration is slow, your deploy blocks on it. If two app instances start simultaneously against a shared DB, they may race (EF has locking, but the interaction with your deploy orchestration needs thought).

### Pattern B — Generate SQL, hand it to a DBA

Take the migration and turn it into a raw SQL script:

```bash
dotnet ef migrations script > migration.sql
```

> **Visual Studio PMC equivalent:** `Script-Migration`

This produces the SQL needed to bring a database from "no migrations applied" to "all migrations applied". Flags let you narrow it:

```bash
# From a specific already-applied migration, up to the latest
dotnet ef migrations script InitialCreate

# An "idempotent" script — safe to run against any state, checks __EFMigrationsHistory
dotnet ef migrations script --idempotent
```

Hand `migration.sql` to whoever runs schema changes in production — a DBA, a release engineer, a CI step that runs against the DB before the app deploys.

**Pros:** The app runs with minimal DB permissions (select/insert/update/delete, no DDL). Schema changes are reviewed, logged, scheduled separately from app deploys. No startup race.

**Cons:** Operational overhead — someone has to manage the handoff. Mistakes between "run the SQL" and "deploy the app" produce downtime.

### For this course

Stay on **Pattern A** (`Migrate()` on startup). It's what you have, it's the simplest working model, and it's what you'd pick for any personal project or team of two. Pattern B is worth knowing exists — when your course moves into deployment, you'll revisit.

---

## Acceptance check

You pass this lesson when all of these hold:

1. You've added a `Priority` field to `Todo`, generated `AddTodoPriority`, and applied it.
2. You've run `dotnet ef database update InitialCreate` and confirmed the column was dropped.
3. You've run `dotnet ef migrations remove` on an unapplied migration and confirmed the files were cleanly removed (the `Migrations/` folder and the snapshot are back to the previous state).
4. You can answer in one sentence each: when do you use `migrations remove`? when do you use `database update <PreviousName>`?
5. You've read the output of `dotnet ef migrations script` at least once, even if you don't use it yet.

---

## Where this leaves you

You can evolve a schema, roll back mistakes, and hand raw SQL to a DBA when that's the workflow. Combined with Lessons 1 and 2:

- **Lesson 1** — swap the provider, connection string configuration, env-var vs file.
- **Lesson 2** — migrations exist, `InitialCreate`, seed data as `InsertData`, `Migrate()` replaces `EnsureCreated()`.
- **Lesson 3** — schema changes, rollback, discard, production handoff.

From here, the two natural next destinations are:

- **Concurrency tokens** — optimistic locking on `UpdatedAt` so two processes writing the same row don't lose updates silently.
- **Indexes, constraints, and real Postgres types** — JSONB columns for semi-structured data, `citext` for case-insensitive text, generated columns, `UNIQUE` constraints beyond the primary key. All expressed as `[Index]` attributes, fluent-API calls in `OnModelCreating`, or raw SQL in migrations.

Both live in their own lessons when you need them. The migration machinery you just learned is what delivers either one to a running database.

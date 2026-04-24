# Teacher's checklist — Postgres track

A scannable live-delivery guide for the three Postgres lessons. Keep it open on a second monitor. Each lesson block has: pre-flight, what to demo in order, gotchas to pre-empt, mid-lesson checkpoints, and a closing test.

---

## Before the first Postgres lesson (one-time setup)

- [ ] Confirm students have **`dotnet --list-sdks`** reporting the same major version as the course (.NET 10 for the current projects). EF Core and the `dotnet-ef` tool are version-matched.
- [ ] Students have `dotnet-ef` installed: `dotnet tool install --global dotnet-ef` (or `update` if they have an old one). **Version must match the project's EF Core package** — mismatches produce bewildering errors in Lesson 2. Have them run `dotnet ef --version` and compare against the `EntityFrameworkCore` package version in the csproj.
- [ ] Students have **Docker Desktop running**, OR an existing local Postgres. Course ships `lessons/postgres/infra/docker-compose.yml` for the Docker path; natives bring their own.
- [ ] Students have a **database inspection tool** installed. Recommend **Azure Data Studio** with the Postgres extension. pgAdmin works too. `psql` is fine but terse.
- [ ] Have **Azure Data Studio / pgAdmin open on the projector machine** already connected to the local DB — you'll show table inspection live in Lesson 2.
- [ ] Students have the **Lesson 5** project running (`dotnet run`) and hitting `/api/TodoLists` successfully. If any student is behind, fix before starting this track.
- [ ] Decide how students will get the code: clone the repo or copy `Lesson5 → Postgres1` themselves? Live-typing csproj/namespace renames is dull; consider pre-provisioning `Postgres1/` on the projector so you can start directly on the code that matters.
- [ ] Know where `appsettings.Development.json` lives. Remind students it's committed, because these are **local dev credentials**, not secrets. Production uses env vars — say it verbally when you get there.
- [ ] Open a terminal tab already `cd`'d into `lessons/postgres/infra/`. Saves 30 seconds when you `docker compose up -d` live.

---

## Postgres Lesson 1 — Swap in-memory for Postgres

### Pre-flight (2 min)

- [ ] Copy `lessons/Lesson5 → lessons/postgres/Postgres1` and rename csproj/namespaces, or have it pre-done.
- [ ] Stop any running `Lesson5` instance — port collision will confuse the demo.
- [ ] `docker compose up -d` from `infra/` if that's your path. Run `docker compose ps` live so students see the service come up.

### The narrative beat

Start with the problem in one sentence: **"Every `dotnet run` so far has wiped your data. Today we fix that — same API, real database, same line of code."**

Then split the problem visibly on the whiteboard:
1. **A running Postgres** — infrastructure (Docker or native).
2. **A connection string** — the bridge.
3. **One line of `Program.cs`** — `UseInMemoryDatabase` → `UseNpgsql`.

That's it. The lesson is 90% "hook up the plumbing"; the mental model is the other 10%.

### Demo sequence — in this order

1. [ ] **Install the Npgsql provider** (30 sec):
   ```bash
   dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
   ```
2. [ ] **Optionally remove the in-memory package** — show the csproj diff. Flag that leaving it installed is harmless. Don't dwell.
3. [ ] **Rewrite the DbContext registration in `Program.cs`** — live-type it. Three changes visible together:
   - [ ] `builder.Configuration.GetConnectionString("AppDb")` + `throw` on missing.
   - [ ] `UseInMemoryDatabase("TodoDb")` → `UseNpgsql(connectionString)`.
   - [ ] **Point out: nothing else in the app changes.** Controllers, DbSet, LINQ — all provider-agnostic.
4. [ ] **Add the connection string** to `appsettings.Development.json`. Walk through the shape:
   ```
   Host=localhost;Port=5433;Database=bank;Username=postgres;Password=postgres
   ```
   - [ ] **Type a deliberately wrong port (5432) first and run.** Students will see the Npgsql connection refused error. Fix to 5433. **This is the cleanest moment to teach "the error points at the config, not the code."**
5. [ ] **The `EnsureCreated()` band-aid.** Show it in the scope block. Say out loud: "This creates tables if the DB is empty. It ignores migrations. It's a dead end, and Lesson 2 replaces it."
6. [ ] **Explain the three config layers** (30 sec, not longer):
   - [ ] `appsettings.json` — committed defaults.
   - [ ] `appsettings.Development.json` — committed dev credentials (what we're using).
   - [ ] Environment variables — **production secrets**. Draw `ConnectionStrings__AppDb` on the whiteboard.

### Mid-lesson checkpoint

```bash
dotnet run
curl http://localhost:5107/api/TodoLists
# → the seeded Inbox + DbSeeder demo lists
```

If the startup fails, **the error text is usually enough** — connection refused (Postgres not running / wrong port), authentication failed (credentials), database doesn't exist (forgot to create `bank`).

### The "data persists" demo — the key moment

1. [ ] `POST` a new TodoList via Scalar or curl.
2. [ ] `Ctrl+C` to stop the app.
3. [ ] `dotnet run` again.
4. [ ] `GET /api/TodoLists` → **the list is still there.**

**Don't rush this step.** It's the payoff for the whole lesson. Let students feel the click.

### Inspect the database — introduce the tool

- [ ] Open Azure Data Studio (already connected on the projector).
- [ ] Show the `TodoLists` and `Todos` tables and their rows.
- [ ] Mention `__EFMigrationsHistory` — **"Lesson 2 uses this."**
- [ ] Show the `psql` equivalent as a one-liner callout for CLI students: `psql -h localhost -p 5433 -U postgres -d bank -c '\dt'`. Note the **double-quote gotcha** for PascalCase identifiers.

### Gotchas to pre-empt

- **⚠ "Connection refused."** Almost always: Postgres isn't running, or the port is wrong. `docker compose ps` (Docker) or `pg_isready -p <port>` (native).
- **⚠ Port 5432 already in use.** A student with a native Postgres on 5432 who starts the compose stack without reading will hit a collision. The compose file uses **5433 on the host** specifically to avoid this — point at it.
- **⚠ "Authentication failed."** Username/password typo in the connection string. Copy-paste from the compose file comment.
- **⚠ "Relation `TodoLists` does not exist."** They kept `EnsureCreated()` but created the DB manually without letting EF create the tables. `DROP DATABASE bank; CREATE DATABASE bank;` and restart the app — EnsureCreated will populate it.
- **⚠ The `__EFMigrationsHistory` table doesn't exist yet.** That's correct for this lesson — `EnsureCreated` doesn't create it; `Migrate` does. Flag this now so it's not a surprise in Lesson 2.
- **⚠ Students paste the connection string into `appsettings.json` instead of `appsettings.Development.json`.** Works, but ships the creds. Flag the distinction.

### Closing test (out loud, together)

"When I hit `/api/TodoLists` just now, what happened?" — walk through the pipeline:
1. ASP.NET read the `AppDb` connection string from `appsettings.Development.json`.
2. Npgsql opened a connection to Postgres on `localhost:5433`.
3. EF Core translated `_context.TodoLists.ToListAsync()` into `SELECT ... FROM "TodoLists"`.
4. Postgres returned the rows; EF hydrated them into `TodoList` objects; the controller serialised them to JSON.

**Nothing in the application code changed vs the in-memory version.** That's the EF Core payoff.

---

## Postgres Lesson 2 — Your first migration

### Pre-flight (2 min)

- [ ] Copy `Postgres1 → Postgres2`, rename csproj/namespaces. **Strip the `// NEW` comments from Postgres1's `Program.cs`** — they're no longer new.
- [ ] Confirm `dotnet ef --version` matches the project's EF Core version. **Mismatch is the #1 source of "it works for me, not for them" — nip it here.**
- [ ] Drop the existing DB table data so `Migrate()` has work to do: `DROP DATABASE bank; CREATE DATABASE bank;` (or `docker compose down -v && docker compose up -d`).

### The narrative beat

**"`EnsureCreated` is a band-aid. It creates tables if the DB is empty and does nothing if it isn't — which means the moment you change a model, your DB and your code are silently out of sync. Migrations are the fix: versioned, explicit, undoable."**

Whiteboard two columns:
- **`EnsureCreated()`** — one-shot, ignores history, can't evolve.
- **`Migrate()`** — applies every pending migration, tracked in `__EFMigrationsHistory`.

Don't run both. That's the rule.

### Demo sequence — in this order

1. [ ] **Install `dotnet-ef`** (if not already done): `dotnet tool install --global dotnet-ef`. **Or** show it's already there. Don't skip the version check.
2. [ ] **Generate the migration** (30 sec):
   ```bash
   dotnet ef migrations add InitialCreate
   ```
   - [ ] **In Visual Studio equivalent:** `Add-Migration InitialCreate` in Package Manager Console. Mention the **Default project dropdown gotcha**.
3. [ ] **Open the generated file.** Walk it section by section:
   - [ ] `CreateTable("TodoLists", ...)` — column list, PK, **don't rush past the `Id` identity column**.
   - [ ] `CreateTable("Todos", ...)` — with the **foreign key constraint** — pause here. This is what the in-memory provider fakes and Postgres actually enforces.
   - [ ] **Scroll to `InsertData` calls.** "This is your `HasData` from Lesson 4 — seeded rows baked into the migration itself." **Conceptual heart of the lesson.**
   - [ ] `Down` — the exact inverse. DropTable. "If you ever roll this back, this is what runs."
4. [ ] **Apply the migration:**
   ```bash
   dotnet ef database update
   ```
5. [ ] **In Azure Data Studio, refresh.** Show:
   - [ ] The tables now exist (`TodoLists`, `Todos`, `__EFMigrationsHistory`).
   - [ ] `SELECT * FROM "__EFMigrationsHistory"` shows one row with `MigrationId = 'InitialCreate'` + the EF Core version.
   - [ ] `SELECT * FROM "TodoLists"` shows the Inbox row — the `HasData` rows are there, inserted by the migration.
6. [ ] **Replace `EnsureCreated()` with `Migrate()`** in `Program.cs`. One-line change; read it out of the file to emphasise.
7. [ ] **Restart the app.** Nothing visible changes — but now every future schema change flows through migrations.

### Mid-lesson checkpoint

```bash
dotnet ef migrations list
# → InitialCreate

psql -h localhost -p 5433 -U postgres -d bank -c 'SELECT * FROM "__EFMigrationsHistory";'
# → one row

curl http://localhost:5107/api/TodoLists
# → the seeded data, same as before
```

If any of the three is wrong: migrations folder missing (didn't run `add`), history empty (didn't run `update`), seed data missing (ran `EnsureCreated` and `Migrate` simultaneously — drop the DB and start over).

### The seed-upsert moment

This is the subtle point students must internalise.

- [ ] Change the Inbox `Description` string in `AppDbContext.OnModelCreating`.
- [ ] Restart the app. **Show that the DB row is unchanged.**
- [ ] Ask: "Why isn't it updating?"
- [ ] Answer: `HasData` is *migration-generation input*, not runtime seeding. The running app never compares it against the DB.
- [ ] `dotnet ef migrations add UpdateInboxDescription` — open the generated file — **show the `UpdateData` call**.
- [ ] `dotnet ef database update`. Refresh the table. Row is now updated.

**The rule to close with: "Every change to seeded data needs its own migration."**

### Gotchas to pre-empt

- **⚠ `dotnet-ef` version mismatch.** Startup / command output hints at "newer than the runtime" or similar. `dotnet tool update --global dotnet-ef` to latest, rerun.
- **⚠ Running `EnsureCreated` AND `Migrate`.** `EnsureCreated` creates the tables outside the migration history; `Migrate` then tries to create them again and throws. **Remove EnsureCreated.**
- **⚠ Assets file not found.** Must `dotnet restore` before `dotnet ef migrations add` on a freshly-renamed project. Harmless; flag once.
- **⚠ "Unable to create a 'DbContext'."** Usually thrown because the connection string is missing and the app can't build. The `dotnet ef` tooling builds and probes the app — a broken startup breaks migrations.
- **⚠ Students commit the `bin/` and `obj/` folders now that migrations live in the project.** Repo `.gitignore` already covers this; verify.
- **⚠ Students think `Migrate()` runs migrations at build time.** It doesn't — it runs at *startup*. Point out the scope block.

### Closing demo — the two-question quiz (out loud)

Ask, don't tell:

1. "If I clone this repo on a fresh machine and run it, does the DB come up?" **Yes — `Migrate()` applies every migration in order against an empty Postgres.**
2. "If my teammate adds a migration, pulls it, runs `dotnet run` — what happens?" **`Migrate()` applies only the new one; previous migrations are skipped via `__EFMigrationsHistory`.**

If the students can answer both, they've got it.

---

## Postgres Lesson 3 — Schema changes and rollbacks

### Pre-flight (2 min)

- [ ] Copy `Postgres2 → Postgres3`, rename. **Strip Postgres2's `// NEW` comment from `Program.cs`** — Migrate is no longer new.
- [ ] Confirm Postgres2's schema is applied: `dotnet ef migrations list` shows `InitialCreate` applied.
- [ ] Keep Azure Data Studio open with the `Todos` table visible — students will see the `Priority` column appear live.

### The narrative beat

**"You now have the migration machinery. Today you actually *evolve* a schema: add a field, generate a migration, read it before applying, and — when you get it wrong — roll it back cleanly."**

Three operations, in this order on the whiteboard:
1. **Add** — the common case.
2. **Rollback of an applied migration** — when you need to undo something in the DB.
3. **Discard of an unapplied migration** — when the generated file is wrong and you don't want it at all.

### Demo sequence — in this order

1. [ ] **Add `public int Priority { get; set; }`** to `Models/Todo.cs`. **Non-nullable int — teach why this forces a `defaultValue` in the migration.**
2. [ ] **Generate the migration:**
   ```bash
   dotnet ef migrations add AddTodoPriority
   ```
3. [ ] **READ THE GENERATED FILE. Do not apply it yet.** Walk through:
   - [ ] `AddColumn<int>(... defaultValue: 0)` — EF backfilled. **Ask: "What if I'd used `int?` instead?"** Answer: nullable column, existing rows get NULL.
   - [ ] `UpdateData(... table: "Todos", keyValue: 1, column: "Priority", value: 0)` — the seeded welcome Todo gets its new column populated. **Students often miss this.**
   - [ ] `Down` — `DropColumn`. **Say out loud: "If this column had production data you cared about, Down would silently delete it. Migrations are reversible, data is not."**
4. [ ] **"Read before apply" is the load-bearing habit.** Say it, write it on the whiteboard, repeat it. A renamed property looks to EF like drop+add — catching that in the generated file is cheap; catching it in prod after `update` is not.
5. [ ] **Apply:**
   ```bash
   dotnet ef database update
   ```
6. [ ] **Refresh Azure Data Studio** — the `Priority` column appears, all existing rows have `0`.

### The rollback demo

Tell a story: "A week passes. You realise `Priority` should have been a string enum, not an int."

1. [ ] `dotnet ef database update InitialCreate` — the target-state model. "Step the DB backwards to exactly this migration."
2. [ ] Refresh Azure Data Studio — `Priority` column is gone. `__EFMigrationsHistory` has only `InitialCreate`.
3. [ ] **Warning moment:** "Your C# code still references `Priority`. The app crashes on the next query. That's expected — rolling back the DB without reverting the code is half the operation."
4. [ ] Revert the model change (remove `Priority`), then `dotnet ef migrations remove` to delete the migration file cleanly.
5. [ ] Restart the app. Back to a clean `InitialCreate`-only state.

### The discard demo

Different story: "You ran `migrations add AddTodoPriority`, read the generated file, realised it's wrong. It's on disk but not applied."

1. [ ] Emphasise: **do not delete the file by hand.** The snapshot (`AppDbContextModelSnapshot.cs`) won't be updated and the next `migrations add` produces nonsense.
2. [ ] `dotnet ef migrations remove` — the tool deletes the migration AND rewinds the snapshot.
3. [ ] Try it after applying it first — **show the refusal.** Safety rail: tool won't remove a migration that's already applied. You must roll back first.

### The dry-run workflow — state it explicitly

Four steps, paste as a block on the whiteboard:

1. `dotnet ef migrations add CandidateChange`
2. Read the generated Up/Down.
3. Right → `dotnet ef database update`. Wrong → `dotnet ef migrations remove`, fix the model, step 1 again.

**That IS the dry-run.** EF has no `--dry-run` flag; it doesn't need one.

### The production story — 5 min, no demo

Show both patterns on the whiteboard:

- **Pattern A: `Migrate()` on startup** (what we have). App has DDL permissions; deploys apply schema. **Good for small teams.**
- **Pattern B: `dotnet ef migrations script --idempotent > migration.sql`** handed to a DBA. App runs with minimal permissions. **Good for large teams / regulated environments.**

Don't demo. Just plant the seed.

### Gotchas to pre-empt

- **⚠ "Data loss" warning when generating a migration.** Read it carefully. Usually a column rename looked like drop+add. Rename the *migration* manually via `RenameColumn` before applying, or restructure the model change.
- **⚠ `migrations remove` refuses.** The migration is applied. Roll back first (`database update <PreviousName>`), then remove.
- **⚠ "Can I delete the migration file manually?"** Don't. The snapshot desynchronises. Students who do this will produce bizarre next migrations. **Always `migrations remove`.**
- **⚠ Rolling back a migration drops columns with data.** Fine on dev DBs; destructive on prod. For prod, roll *forward* with a corrective migration instead.
- **⚠ "`database update <Name>` syntax."** Students try `database update --target` or similar. No flag — the name is a positional argument.
- **⚠ Dropped the whole database to restart.** Fine for dev. Catastrophic muscle-memory for prod. Say it once; move on.

### Closing demo — the three-migration timeline

Draw on the whiteboard:

```
Time →   InitialCreate ── AddTodoPriority ── (you are here)
              │                 │
              Down?            Down?
              drops tables     drops column
```

Ask: "What does `dotnet ef database update InitialCreate` do if I'm at `AddTodoPriority`?" (runs `AddTodoPriority.Down`)
Ask: "What about `dotnet ef database update 0`?" (runs every `Down` in reverse; strips the DB to empty)
Ask: "How do I get from empty back to latest?" (`dotnet ef database update` with no argument)

---

## Live-demo data cheat sheet

### Connection string (default)

```
Host=localhost;Port=5433;Database=bank;Username=postgres;Password=postgres
```

### Commands you'll repeat (paste-ready)

```bash
# Start / stop / reset the Docker DB
cd lessons/postgres/infra
docker compose up -d
docker compose ps
docker compose down          # stop, keep data
docker compose down -v       # stop, wipe data (fresh next time)

# Migrations
dotnet ef migrations add <Name>
dotnet ef migrations list
dotnet ef migrations remove
dotnet ef database update
dotnet ef database update <MigrationName>    # roll back to that migration
dotnet ef database update 0                   # roll back everything

# Inspect
psql -h localhost -p 5433 -U postgres -d bank -c '\dt'
psql -h localhost -p 5433 -U postgres -d bank -c 'SELECT * FROM "__EFMigrationsHistory";'
```

### The seeded `HasData` row (Inbox)

```sql
SELECT "Id", "Title", "Description" FROM "TodoLists" WHERE "Id" = 1;
-- Id | Title | Description
-- 1  | Inbox | Your default list — cannot be deleted.
```

---

## If you only have time for ONE demo in each lesson

- **Lesson 1:** Create a TodoList, stop the app, restart, hit the list again — **the data is still there**. One minute, pure payoff. This single demo justifies the whole track.
- **Lesson 2:** The migration file walkthrough — specifically the `InsertData` calls corresponding to `HasData`. **Students understand the seed-as-schema point here or they don't understand migrations at all.**
- **Lesson 3:** `dotnet ef database update InitialCreate` rolls back the Priority column live, in Azure Data Studio, in front of them. One command, visible schema change, the rollback story in 30 seconds.

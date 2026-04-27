# Exercise 3 — Seed the in-memory database

**Reference lesson:** [`../lessons/3_lesson_seed_database.md`](../lessons/3_lesson_seed_database.md)

By the end: every `dotnet run` starts your API with three pre-populated accounts, and the seeder is idempotent.

---

## Core

### 3.1 Write `DbSeeder.Seed`

Create `Data/DbSeeder.cs` — a `public static class` with one `public static void Seed(AppDbContext db)` method.

The method:

- Returns early if `db.Accounts.Any()` is already true (idempotent).
- Adds exactly three accounts via `db.Accounts.AddRange(...)`:
  - `{ AccountNumber = "ACC-1000" }`
  - `{ AccountNumber = "ACC-1001" }`
  - `{ AccountNumber = "ACC-1002" }`
- Calls `db.SaveChanges()`.

### 3.2 Call the seeder from `Program.cs`

After `var app = builder.Build();`, create a scope, resolve `AppDbContext`, call `db.Database.EnsureCreated()`, then `DbSeeder.Seed(db)`. Match the lesson's shape exactly.

### Acceptance check

Restart the app fresh. Then:

```bash
curl http://localhost:<port>/api/Accounts
# → three accounts with accountNumbers ACC-1000, ACC-1001, ACC-1002
#   (exact createdAt values differ; the accountNumbers must match)
```

Restart again with `Ctrl+C` + `dotnet run`. Hit the same endpoint — you should see **exactly three accounts**, not six. That's idempotency.

---

## Stretch

### 3.S1 — Seed Branches

(Requires 1.S1.) Extend `Seed` to also insert three branches via a similar guard (`if (db.Branches.Any()) return;`). Use:

- `{ Name = "London HQ", Address = "1 Threadneedle St" }`
- `{ Name = "Manchester", Address = "12 King St" }`
- `{ Name = "Edinburgh", Address = "45 George St" }`

You may put the Branch block in the same `Seed` method — just use its own `if (db.Branches.Any())` guard separate from the Accounts one, so each block is independently idempotent.

#### Acceptance

```bash
curl http://localhost:<port>/api/Branches
# → three branches, one per seeded row
```

### 3.S2 — Seed Customers

(Requires 1.S2.) Same pattern. Three customers:

- `{ FullName = "Ada Lovelace", Email = "ada@example.com" }`
- `{ FullName = "Alan Turing",  Email = "alan@example.com" }`
- `{ FullName = "Grace Hopper", Email = "grace@example.com" }`

#### Acceptance

```bash
curl http://localhost:<port>/api/Customers
# → three customers
```

### 3.S3 — Prove idempotency end-to-end

Without restarting, `DELETE` one seeded account. Restart the app. Hit `GET /api/Accounts` — the deleted account **does not** come back, because the in-memory DB is wiped on restart and the fresh seeder populates three rows every time.

Then keep the app running and **do not** delete anything. Restart again. Confirm the count is still three (not six). Write a one-line comment in `DbSeeder.cs` explaining why the `Any()` guard matters when you switch to a real database later.

#### Acceptance

After the second restart with no intervening writes, `GET /api/Accounts` still returns exactly three accounts. The comment exists.

---

## Done?

Your app starts with known data every time. No more empty databases when you open Scalar.

Commit, then move on to [Exercise 4](4_updated_at.md).

# Exercise 5 — `Transaction` entity and the `IHasUpdatedAt` interface

**Reference lesson:** [`../lessons/5_lesson_todo_entity_and_relations.md`](../lessons/5_lesson_todo_entity_and_relations.md)

By the end: a `Transaction` entity linked to `Account` by a foreign key, a marker interface that controls which entities get stamped, and seeded opening-deposit transactions.

---

## Core

### 5.1 Define the `IHasUpdatedAt` interface

Create `Models/IHasUpdatedAt.cs` with exactly:

```csharp
namespace BankingApi.Models;

public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}
```

### 5.2 Make `Account` implement the interface

`Account` already has `UpdatedAt` from Exercise 4. Add `: IHasUpdatedAt` to the class declaration — that's the only change.

### 5.3 Create the `Transaction` entity

`Models/TransactionType.cs`:

```csharp
namespace BankingApi.Models;

public enum TransactionType { Credit, Debit }
```

`Models/Transaction.cs` — exactly these fields:

| Field         | Type              | Default                    |
|---------------|-------------------|----------------------------|
| `Id`          | `int`             | —                          |
| `Type`        | `TransactionType` | —                          |
| `Amount`      | `decimal`         | —                          |
| `Description` | `string`          | `string.Empty`             |
| `Timestamp`   | `DateTime`        | `DateTime.UtcNow`          |
| `AccountId`   | `int`             | — (foreign key)            |
| `Account`     | `Account?`        | — (navigation, nullable)   |

**`Transaction` does NOT implement `IHasUpdatedAt`.** Transactions are historical — once recorded, they don't change. Do not add an `UpdatedAt` property.

Also add an inverse navigation on `Account`:

```csharp
public List<Transaction>? Transactions { get; set; }
```

Nullable on purpose — see the lesson's design note.

### 5.4 Register the DbSet and generalise `SaveChanges`

In `AppDbContext`:

- Add `public DbSet<Transaction> Transactions => Set<Transaction>();`
- Change the `StampUpdatedAt` loop from `ChangeTracker.Entries<Account>()` to `ChangeTracker.Entries<IHasUpdatedAt>()`.

That one-line change is the whole point of the interface: the loop now stamps any entity that *opts in* by implementing the interface. `Transaction` is silently ignored.

### 5.5 Seed opening-deposit transactions

Extend `DbSeeder.Seed` with a **third guarded block** for transactions (`if (db.Transactions.Any()) return;` inside the block). Use the two-phase pattern:

- `SaveChanges` **after** the existing account block (if new accounts were inserted) so EF assigns their `Id` values.
- Then add one `Credit` transaction per seeded account with `Amount = 1000m`, `Description = "Opening deposit"`, `AccountId = <that account's Id>`. Use `AddRange`, then one `SaveChanges`.

### Acceptance check

Restart the app. Then:

```bash
# All seeded transactions visible via the (scaffolded-by-EF-default) DbSet
# We don't have a TransactionsController yet (Exercise 6 creates it), so use the lesson's
# trick — query via Accounts won't show them until Exercise 6 either. Instead, verify
# via a PUT that stamps UpdatedAt correctly:

curl -X PUT http://localhost:<port>/api/Accounts/1 \
     -H "Content-Type: application/json" \
     -d '{"id":1,"accountNumber":"ACC-1000-v2","createdAt":"2026-01-01T00:00:00Z"}'
# → 204 No Content

curl http://localhost:<port>/api/Accounts/1
# → { ..., "accountNumber":"ACC-1000-v2", "updatedAt":"2026-...", "transactions": null }
```

Two things must be true:

- `updatedAt` is populated on the account — proves the interface filter works for `Account`.
- `transactions` is `null` (not an empty array) — proves we haven't loaded them, which is honest (Exercise 6 wires up the include).

The app must also start without errors. If you get a runtime error about missing FK or DbSet, re-read 5.3 and 5.4.

---

## Stretch

### 5.S1 — Make Branch and Customer implement the interface

(Requires 4.S2.) Add `: IHasUpdatedAt` to both classes. If you copy-pasted the stamping loop in 4.S2, delete the duplicates — the single `Entries<IHasUpdatedAt>()` loop now covers everything.

#### Acceptance

```bash
curl -X PUT http://localhost:<port>/api/Branches/1 \
     -H "Content-Type: application/json" \
     -d '{"id":1,"name":"London HQ 2","address":"1 Threadneedle St","createdAt":"2026-01-01T00:00:00Z"}'
curl http://localhost:<port>/api/Branches/1
# → { ..., "updatedAt":"2026-..." }
```

Same for Customer. The `AppDbContext` has exactly **one** `StampUpdatedAt` method regardless of how many entities implement the interface.

### 5.S2 — Add `BranchId` foreign key on `Account`

(Requires 1.S1.) Give every account a branch:

- Add `public int? BranchId { get; set; }` and `public Branch? Branch { get; set; }` to `Account`. Nullable FK so existing accounts without a branch don't break.
- Add `public List<Account>? Accounts { get; set; }` as the inverse navigation on `Branch`.
- Extend `DbSeeder` to set `BranchId` on each seeded account — assign all three seeded accounts to the first seeded branch. Use the two-phase pattern: save branches first so EF assigns their Ids.

Same one-to-many pattern as Account → Transaction, just with different entities.

#### Acceptance

```bash
curl http://localhost:<port>/api/Accounts/1
# → { ..., "branchId": 1, ... }
```

The `branchId` is populated in the response. (The `branch` navigation is null because we haven't asked for it; that's correct — Exercise 6 introduces `.Include(a => a.Branch)`.)

---

## Done?

The interface works. `Account` gets stamped; `Transaction` doesn't; `Branch` and `Customer` opt in or stay out as you wish. One loop, infinite reach.

Commit, then move on to [Exercise 6](6_includes_and_cycles.md).

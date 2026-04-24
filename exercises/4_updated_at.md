# Exercise 4 — Auto-stamp `UpdatedAt`

**Reference lesson:** [`../lessons/4_lesson_auto_stamp_updated_at.md`](../lessons/4_lesson_auto_stamp_updated_at.md)

By the end: every `PUT` on an account sets `UpdatedAt` automatically — in `SaveChanges`, not in the controller.

---

## Core

### 4.1 Add `UpdatedAt` to `Account`

Add one property to `Models/Account.cs`:

```csharp
public DateTime? UpdatedAt { get; set; }
```

Nullable because a freshly created account has never been updated.

### 4.2 Override `SaveChanges` on `AppDbContext`

Override both `SaveChanges()` and `SaveChangesAsync(CancellationToken)` — exactly as the lesson shows. Stamp only entries in `EntityState.Modified`. At this stage the loop is typed to `Account` specifically:

```csharp
foreach (var entry in ChangeTracker.Entries<Account>())
```

(Lesson 5 will generalise this via an interface — leave it concrete for now.)

### Acceptance check

```bash
# Fresh start — UpdatedAt is null
curl http://localhost:<port>/api/Accounts/1
# → { ..., "updatedAt": null }

# PUT to change the account number
curl -X PUT http://localhost:<port>/api/Accounts/1 \
     -H "Content-Type: application/json" \
     -d '{"id":1,"accountNumber":"ACC-1000-renamed","createdAt":"2026-01-01T00:00:00Z"}'
# → 204 No Content

# Fetch again — UpdatedAt is populated, CreatedAt unchanged
curl http://localhost:<port>/api/Accounts/1
# → { ..., "accountNumber":"ACC-1000-renamed", "updatedAt":"2026-..." }
```

The controller action didn't change. The stamping happened inside `SaveChangesAsync`.

---

## Stretch

### 4.S1 — Prove `Added` rows are not stamped

Create a brand-new account via POST and fetch it back:

```bash
curl -X POST http://localhost:<port>/api/Accounts \
     -H "Content-Type: application/json" \
     -d '{}'
# → { "id":N, ..., "updatedAt": null }
```

`updatedAt` must be `null` on the response. If it's a timestamp, your stamping logic is wrong — it should only fire on `EntityState.Modified`, not `Added`.

No code change required if your implementation is correct. This is a behavioural check — writing it down here to make sure you notice.

### 4.S2 — Add `UpdatedAt` to Branch and Customer

(Requires 1.S1 and/or 1.S2.) Add the same `DateTime? UpdatedAt { get; set; }` property to `Branch.cs` and `Customer.cs`.

Here's the honest trade-off: your `StampUpdatedAt` method is typed to `Account`, so Branch and Customer **won't be stamped** by it. You've got two choices:

- **Accept the limitation for now.** Add the field; don't touch `SaveChanges`. PUTting a branch won't stamp its `UpdatedAt`. You'll fix this properly in Exercise 5 via a marker interface.
- **Copy-paste the loop.** Duplicate the `StampUpdatedAt` loop for Branch and Customer. It works — and feels immediately ugly. That *feeling* is the reason Lesson 5 introduces the interface.

Either is fine. Pick one, note your choice in a comment in `AppDbContext.cs`, and move on. Exercise 5 will resolve both.

#### Acceptance

The field exists on both entities. Your chosen approach is documented in `AppDbContext.cs`.

---

## Done?

Accounts now audit themselves on every write. Whether or not Branch and Customer follow suit, Exercise 5 pulls the whole thing together with an interface.

Commit, then move on to [Exercise 5](5_transaction_and_relations.md).

# Exercise 7 — Query parameters: filtering and opt-in loading

**Reference lesson:** [`../lessons/7_lesson_query_params_and_filtering.md`](../lessons/7_lesson_query_params_and_filtering.md)

By the end: `?include=transactions` is opt-in (not automatic), `?type=Credit` filters transactions by kind, and all query knobs come from a bound `AccountQuery` class.

---

## Core

### 7.1 Make `?include=transactions` opt-in

In `AccountsController.GetAccount`, pull the `.Include(a => a.Transactions)` out of the unconditional path. Accept a `[FromQuery] string? include`, parse it (same helper as the lesson's `ParseInclude`), and only call `.Include(...)` when the set contains `"transactions"`.

Do the same in `GetAccounts` (the list endpoint).

### 7.2 Add `?type=Credit|Debit` filtered Include

On the **single-account** endpoint, accept `[FromQuery] TransactionType? type` alongside `include`. Nullable enum — three states: `Credit`, `Debit`, or unspecified (both).

When the caller asked for `?include=transactions`:

- If `type.HasValue`, use a **filtered Include**: `.Include(a => a.Transactions!.Where(t => t.Type == wantType))`.
- Otherwise, plain `.Include(a => a.Transactions)`.

Follow the same `if (type.HasValue) { var wantType = type.Value; ... }` pattern as the lesson — capture the non-nullable value into a local so the EF expression tree is clean.

### 7.3 Refactor to `AccountQuery`

Create `Models/AccountQuery.cs`:

```csharp
namespace BankingApi.Models;

public class AccountQuery
{
    public string? Include { get; set; }
    public TransactionType? Type { get; set; }
}
```

Change both `GetAccount` and `GetAccounts` to accept `[FromQuery] AccountQuery query` instead of two separate parameters. The URL grammar doesn't change — `?include=transactions&type=Credit` still works.

Also add a matching filter to `TransactionsController.GetTransactions`: `[FromQuery] TransactionType? type`, apply `.Where(t => t.Type == type.Value)` when supplied.

### Acceptance check

```bash
# 1) Lean — no transactions loaded by default
curl "http://localhost:<port>/api/Accounts/1"
# → { ..., "transactions": null }

# 2) Opt-in — transactions included
curl "http://localhost:<port>/api/Accounts/1?include=transactions"
# → { ..., "transactions": [ ... all of them ... ] }

# 3) Filtered include — only credits
curl "http://localhost:<port>/api/Accounts/1?include=transactions&type=Credit"
# → transactions array contains only entries with "type": 0

# 4) Filtered list — only debits across all accounts
curl "http://localhost:<port>/api/Transactions?type=Debit"
# → every entry has "type": 1

# 5) Case-insensitive binding works (ASP.NET handles this for enums automatically)
curl "http://localhost:<port>/api/Accounts/1?include=TRANSACTIONS&type=credit"
# → same as (3)
```

All five responses must match the shapes above.

---

## Stretch

### 7.S1 — Combine includes: `?include=transactions,branch`

(Requires 5.S2 and 6.S1.) Extend `AccountQuery` so the comma-parsed set also accepts `"branch"`. Chain a conditional `.Include(a => a.Branch)` when the set contains it.

#### Acceptance

```bash
curl "http://localhost:<port>/api/Accounts/1?include=transactions,branch"
# → both "transactions" and "branch" populated
```

### 7.S2 — `?minAmount=` filter on transactions

On `TransactionsController.GetTransactions`, accept `[FromQuery] decimal? minAmount` and apply `.Where(t => t.Amount >= minAmount.Value)` when supplied. The tri-state rule is the same as `bool?` and `TransactionType?`.

#### Acceptance

```bash
curl "http://localhost:<port>/api/Transactions?minAmount=500"
# → only transactions whose Amount is >= 500
```

### 7.S3 — `?since=` date filter on transactions

Accept `[FromQuery] DateTime? since`. Apply `.Where(t => t.Timestamp >= since.Value)` when supplied. ASP.NET binds ISO-8601 strings into `DateTime` automatically, so callers can pass `?since=2026-04-01`.

#### Acceptance

```bash
curl "http://localhost:<port>/api/Transactions?since=2026-01-01"
# → every returned Timestamp is >= 2026-01-01

# Combined with minAmount
curl "http://localhost:<port>/api/Transactions?since=2026-01-01&minAmount=100"
# → intersection of both filters
```

---

## Done?

Callers control what they get. Lean responses are the default; richer responses arrive on request.

Commit, then move on to [Exercise 8](8_dtos.md).

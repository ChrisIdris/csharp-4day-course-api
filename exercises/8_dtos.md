# Exercise 8 — DTOs and a transfer endpoint

**Reference lesson:** [`../lessons/8_lesson_dtos.md`](../lessons/8_lesson_dtos.md)

By the end: your controllers return DTOs (not entities), responses include a computed `Balance` only when transactions are loaded, and a single `transfer` endpoint moves money between two accounts.

---

## Core

### 8.1 Create the response DTOs

Make a new folder `Dtos/`.

`Dtos/TransactionResponse.cs` — a `record`, positional:

```csharp
namespace BankingApi.Dtos;
using BankingApi.Models;

public record TransactionResponse(
    int Id,
    TransactionType Type,
    decimal Amount,
    string Description,
    DateTime Timestamp,
    int AccountId
)
{
    public static TransactionResponse FromEntity(Transaction t) =>
        new(t.Id, t.Type, t.Amount, t.Description, t.Timestamp, t.AccountId);
}
```

No `Account` back-reference. The DTO simply doesn't have that property.

`Dtos/AccountResponse.cs`:

```csharp
namespace BankingApi.Dtos;
using BankingApi.Models;

public record AccountResponse(
    int Id,
    string AccountNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    decimal? Balance,
    List<TransactionResponse>? Transactions
)
{
    public static AccountResponse FromEntity(Account a)
    {
        decimal? balance = a.Transactions == null
            ? null
            : a.Transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount)
              - a.Transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);

        return new AccountResponse(
            a.Id,
            a.AccountNumber,
            a.CreatedAt,
            a.UpdatedAt,
            balance,
            a.Transactions?.Select(TransactionResponse.FromEntity).ToList()
        );
    }
}
```

`Balance` is `decimal?` on purpose: if `Transactions` weren't loaded (caller didn't pass `?include=transactions`), we don't know the balance, so we return `null`. That's honest.

### 8.2 Rewire the controllers to return DTOs

In `AccountsController`:

- Change `GetAccount`, `GetAccounts`, `PostAccount` return types from `TodoList`-shaped to `AccountResponse`-shaped (`ActionResult<AccountResponse>`, `ActionResult<IEnumerable<AccountResponse>>`).
- At the end of each action, project through `AccountResponse.FromEntity`.
- `PostAccount` uses `CreatedAtAction(nameof(GetAccount), new { id = account.Id }, AccountResponse.FromEntity(account))`.

Do the same for `TransactionsController` using `TransactionResponse`.

`PUT` and `DELETE` don't change — they return `204 No Content`, no body to project.

### 8.3 Add the `transfer` endpoint

On `AccountsController`, add a sub-route action:

```csharp
public record TransferRequest(int TargetAccountId, decimal Amount, string Description);

[HttpPost("{id}/transfer")]
public async Task<ActionResult<IEnumerable<TransactionResponse>>> Transfer(int id, TransferRequest request)
{
    if (request.Amount <= 0)
    {
        return BadRequest("Amount must be positive.");
    }

    var source = await _context.Accounts
        .Include(a => a.Transactions)
        .FirstOrDefaultAsync(a => a.Id == id);
    var target = await _context.Accounts.FindAsync(request.TargetAccountId);

    if (source is null || target is null)
    {
        return NotFound();
    }

    var sourceBalance =
        source.Transactions!.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount)
        - source.Transactions!.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);

    if (sourceBalance < request.Amount)
    {
        return Problem(
            detail: "Insufficient funds.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var debit = new Transaction
    {
        Type = TransactionType.Debit,
        Amount = request.Amount,
        Description = $"Transfer to {target.AccountNumber}: {request.Description}",
        AccountId = source.Id
    };
    var credit = new Transaction
    {
        Type = TransactionType.Credit,
        Amount = request.Amount,
        Description = $"Transfer from {source.AccountNumber}: {request.Description}",
        AccountId = target.Id
    };

    _context.Transactions.AddRange(debit, credit);
    await _context.SaveChangesAsync();

    return Ok(new[] { TransactionResponse.FromEntity(debit), TransactionResponse.FromEntity(credit) });
}
```

One `SaveChanges` covers both inserts — that's as atomic as the in-memory provider allows. A real database transaction (later lesson) would wrap this.

### Acceptance check

```bash
# 1) List responses use AccountResponse — no "transactions" key unless asked
curl "http://localhost:<port>/api/Accounts"
# → array of objects, each with "balance": null (we didn't include transactions)

# 2) Single account with transactions — Balance is computed and present
curl "http://localhost:<port>/api/Accounts/1?include=transactions"
# → { ..., "balance": 1000.00, "transactions": [ ... ] }
#   (balance = sum of credits minus sum of debits, for the included rows)

# 3) Transfer 100 from account 1 to account 2
curl -X POST "http://localhost:<port>/api/Accounts/1/transfer" \
     -H "Content-Type: application/json" \
     -d '{"targetAccountId":2,"amount":100,"description":"rent"}'
# → array of 2 TransactionResponse objects — one Debit, one Credit, matching amounts

# 4) Confirm both balances moved
curl "http://localhost:<port>/api/Accounts/1?include=transactions"
# → balance = 900.00
curl "http://localhost:<port>/api/Accounts/2?include=transactions"
# → balance = 1100.00

# 5) Insufficient funds returns 400
curl -X POST "http://localhost:<port>/api/Accounts/1/transfer" \
     -H "Content-Type: application/json" \
     -d '{"targetAccountId":2,"amount":99999,"description":"too much"}'
# → 400 with a Problem payload mentioning "Insufficient funds"
```

All five must behave as shown.

---

## Stretch

### 8.S1 — DTOs for Branch and Customer

(Requires 1.S1 and/or 1.S2.) Write `BranchResponse` and `CustomerResponse` records with `FromEntity` methods. Rewire both controllers. Nothing new to learn — reps of the same pattern.

#### Acceptance

```bash
curl "http://localhost:<port>/api/Branches/1"
# → response shape is your DTO, not the raw entity
```

### 8.S2 — Summary response shape via `?shape=summary`

Add an `AccountSummaryResponse` record with only `Id`, `AccountNumber`, `Balance` (no `Transactions`, no `CreatedAt`, no `UpdatedAt`). Accept a `[FromQuery] string? shape` on `GetAccounts`; when `shape == "summary"`, project to `AccountSummaryResponse` instead of `AccountResponse`.

Return type becomes `ActionResult<object>` or `ActionResult<IEnumerable<object>>` — the caller gets one of two shapes depending on the query param.

#### Acceptance

```bash
curl "http://localhost:<port>/api/Accounts?shape=summary&include=transactions"
# → array of summary objects — three fields, nothing else
```

### 8.S3 — Many-to-many: `Customer ↔ Account`

(Requires 1.S2 and 3.S2.) Banks have **joint accounts** — one account owned by multiple customers, one customer holding multiple accounts. Implement this with EF Core.

The task:

1. Read the EF Core docs on many-to-many relationships: [Many-to-many relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many). EF supports an automatic join entity — you don't need to declare one unless you want extra columns.
2. Add `public List<Account>? Accounts { get; set; }` to `Customer` and `public List<Customer>? Customers { get; set; }` to `Account`.
3. Extend the seeder: make Ada Lovelace a customer on account `ACC-1000`, and also on `ACC-1001` (jointly with Alan Turing).
4. Add a new endpoint on `CustomersController`: `GET /api/Customers/{id}/accounts` that returns every account the customer holds, as `AccountResponse`s. Use `.Include(c => c.Accounts).ThenInclude(a => a.Transactions)` if you want balances computed.

#### Acceptance

```bash
curl "http://localhost:<port>/api/Customers/1/accounts"
# → array of AccountResponse objects — Ada's two accounts, balances populated

curl "http://localhost:<port>/api/Customers/2/accounts"
# → array with Alan's account(s), including the shared one
```

---

## Done?

Your API's wire format is now fully decoupled from the database. DTOs control exactly what goes out. The transfer endpoint is the compound-operation pattern in the wild. If you did the many-to-many stretch, you've shipped a real bank feature.

That's the end of the exercise track. Commit, and go read ahead — Lesson 9 (validation) is next.

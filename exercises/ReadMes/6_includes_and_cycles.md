# Exercise 6 — Returning related records, and fixing the cycle that follows

**Reference lesson:** [`../lessons/6_lesson_related_records_and_cycles.md`](../lessons/6_lesson_related_records_and_cycles.md)

By the end: a `TransactionsController`, an account endpoint that eager-loads its transactions in one query, and the serialisation cycle that appears (and its fix).

---

## Core

### 6.1 Scaffold `TransactionsController`

Same command shape as the accounts controller, different model:

```bash
dotnet aspnet-codegenerator controller \
  -name TransactionsController \
  -async -api \
  -m BankingApi.Models.Transaction \
  -dc BankingApi.Data.AppDbContext \
  -outDir Controllers
```

Five standard actions, generated for you. Don't edit them yet.

### 6.2 Eager-load `Transactions` on `GET /api/Accounts/{id}`

Edit `AccountsController.GetAccount` to `.Include(a => a.Transactions)`:

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Account>> GetAccount(int id)
{
    var account = await _context.Accounts
        .Include(a => a.Transactions)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (account == null) return NotFound();
    return account;
}
```

(Note the switch from `FindAsync` to `FirstOrDefaultAsync` — `FindAsync` can't chain `Include`.)

### 6.3 Hit the cycle, then fix it

Run the app and:

```bash
curl http://localhost:<port>/api/Accounts/1
```

You'll get a **500** with a `JsonException: A possible object cycle was detected` in the server log. Re-read the lesson's "Why does this happen?" section if it's not obvious why.

Fix by adding `[JsonIgnore]` to `Transaction.Account`:

```csharp
using System.Text.Json.Serialization;

public class Transaction : /* no IHasUpdatedAt */
{
    // ...existing fields...

    public int AccountId { get; set; }

    [JsonIgnore]
    public Account? Account { get; set; }
}
```

### Acceptance check

```bash
curl http://localhost:<port>/api/Accounts/1
```

The response must be a valid 200 OK with this shape:

```json
{
  "id": 1,
  "accountNumber": "ACC-1000",
  "createdAt": "...",
  "updatedAt": null,
  "transactions": [
    {
      "id": 1,
      "type": 0,
      "amount": 1000.00,
      "description": "Opening deposit",
      "timestamp": "...",
      "accountId": 1
    }
  ]
}
```

No `"account"` key inside the transaction object. If it's there, `[JsonIgnore]` isn't applied.

Also verify `GET /api/Transactions` still works — since it doesn't `.Include(t => t.Account)`, there's no cycle to trip over in the first place.

---

## Stretch

### 6.S1 — Eager-load `Branch` too

(Requires 5.S2.) Chain a second include:

```csharp
var account = await _context.Accounts
    .Include(a => a.Transactions)
    .Include(a => a.Branch)
    .FirstOrDefaultAsync(a => a.Id == id);
```

If `Branch` has an inverse `Accounts` navigation, it's another cycle waiting to happen — add `[JsonIgnore]` on `Branch.Accounts` (or on `Account.Branch`, your call; document which you chose and why in a one-line comment).

#### Acceptance

```bash
curl http://localhost:<port>/api/Accounts/1
# → response has both "transactions": [...] and "branch": { ... } populated
```

No cycle error. The attribute-carrying side is absent from its parent's payload.

### 6.S2 — Try `ReferenceHandler.IgnoreCycles` globally

Temporarily add this to `Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
```

Remove the `[JsonIgnore]` from `Transaction.Account` (and from `Branch.Accounts` if you added it in S1) — temporarily.

Run the same acceptance check as 6.3 core. The response is valid JSON but different: the back-reference appears as `"account": null` (instead of being absent entirely).

**Write a one-line comment** in `Program.cs` stating which of the two approaches you'd keep in a real bank API, and **why**. Then restore the `[JsonIgnore]` approach (delete the `AddJsonOptions` block and re-add the attributes).

#### Acceptance

You've seen both JSON shapes and made a written choice. The final state of the project uses `[JsonIgnore]`.

---

## Done?

Accounts return their transactions inline without an N+1 storm. The cycle is solved. The API now has two related resources, each with its own controller.

Commit, then move on to [Exercise 7](7_query_params.md).

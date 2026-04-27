# Exercise 1 — Your first `BankingApi` project

**Reference lesson:** [`../lessons/1_lesson_new_api_project.md`](../lessons/1_lesson_new_api_project.md)

By the end: a running ASP.NET Core Web API with a single `Account` resource, full CRUD scaffolded, backed by the in-memory EF Core provider.

---

## Core

### 1.1 Create the project

From `exercises/`, run the lesson's step 1 command with `BankingApi` as the name:

```bash
dotnet new webapi --use-controllers -n BankingApi -o BankingApi
cd BankingApi
```

Delete the `WeatherForecast` sample (the two files from lesson 1 step 2).

Install the EF Core + scaffolding packages from lesson 1 step 3. All five are required, including the `Microsoft.EntityFrameworkCore.SqlServer` quirk the lesson calls out — the scaffolder demands it even on in-memory.

If you don't already have the scaffolding tool globally installed, do it now:

```bash
dotnet tool install -g dotnet-aspnet-codegenerator
```

### 1.2 Write the `Account` entity

Create `Models/Account.cs` with exactly these fields:

| Field           | Type       | Default                   |
|-----------------|------------|---------------------------|
| `Id`            | `int`      | —                         |
| `AccountNumber` | `string`   | `string.Empty`            |
| `CreatedAt`     | `DateTime` | `DateTime.UtcNow`         |

> Ownership (who the account belongs to) is deliberately missing — that arrives with authentication in a later lesson.

### 1.3 `AppDbContext`, registration, controller

- `Data/AppDbContext.cs` — exposes `DbSet<Account> Accounts => Set<Account>();`
- `Program.cs` — register with `options.UseInMemoryDatabase("BankDb")`.
- Scaffold the controller: `-name AccountsController`, `-m BankingApi.Models.Account`, `-dc BankingApi.Data.AppDbContext`.

### Acceptance check

Run the app (`dotnet run`) and note the port from the log (e.g. `5107`). In a second terminal:

```bash
# 1) List — empty on a fresh start
curl http://localhost:<port>/api/Accounts
# → []

# 2) Create
curl -X POST http://localhost:<port>/api/Accounts \
     -H "Content-Type: application/json" \
     -d '{"accountNumber":"ACC-1000"}'
# → {"id":1,"accountNumber":"ACC-1000","createdAt":"..."}

curl -X POST http://localhost:5247/api/Accounts -H "Content-Type: application/json" -d '{\"accountNumber\":\"ACC-1000\"}'

# 3) Fetch it back
curl http://localhost:<port>/api/Accounts/1
# → same shape as the POST response
```

All three responses must match the shapes above. `createdAt` differs run-to-run — only the key must be present with an ISO-8601 timestamp.

---

## Stretch

### 1.S1 — Add a `Branch` entity with its own controller

Banks have branches. Give your API a second resource:

- `Models/Branch.cs` with fields: `Id` (int), `Name` (string, default `string.Empty`), `Address` (string?, nullable), `CreatedAt` (DateTime, default `DateTime.UtcNow`).
- Add `public DbSet<Branch> Branches => Set<Branch>();` to `AppDbContext`.
- Scaffold `BranchesController`: `-name BranchesController`, `-m BankingApi.Models.Branch`, `-dc BankingApi.Data.AppDbContext`.

No relationship to `Account` yet — `Branch` stands alone for now.

#### Acceptance

```bash
curl -X POST http://localhost:<port>/api/Branches \
     -H "Content-Type: application/json" \
     -d '{"name":"London HQ","address":"1 Threadneedle St"}'
# → {"id":1,"name":"London HQ","address":"1 Threadneedle St","createdAt":"..."}

curl http://localhost:<port>/api/Branches
# → [{ ... the branch you just created ... }]
```

### 1.S2 — Add a `Customer` entity with its own controller

Same exercise, third entity:

- `Models/Customer.cs` with fields: `Id` (int), `FullName` (string, default `string.Empty`), `Email` (string?, nullable), `CreatedAt` (DateTime, default `DateTime.UtcNow`).
- Add `public DbSet<Customer> Customers => Set<Customer>();` to `AppDbContext`.
- Scaffold `CustomersController`.

#### Acceptance

```bash
curl -X POST http://localhost:<port>/api/Customers \
     -H "Content-Type: application/json" \
     -d '{"fullName":"Grace Hopper","email":"grace@example.com"}'
# → {"id":1,"fullName":"Grace Hopper","email":"grace@example.com","createdAt":"..."}
```

### 1.S3 — Auto-generate the account number on POST

Your Bank console project assigned account numbers like `ACC-1000`, `ACC-1001` automatically. Do the same on the server side.

**The rule:**

In `AccountsController.PostAccount`, before `SaveChangesAsync`, overwrite whatever the client sent as `accountNumber` with this:

```csharp
account.AccountNumber = $"ACC-{_context.Accounts.Count() + 1000}";
```

That's it. This implementation is deliberately simple — it reuses numbers if accounts are deleted. Good enough for the in-memory single-process API; a real bank would use a DB sequence.

#### Acceptance

```bash
# POST with an empty body — server fills in the account number
curl -X POST http://localhost:<port>/api/Accounts \
     -H "Content-Type: application/json" \
     -d '{}'
# → { "id":N, "accountNumber":"ACC-10NN", ... }   (NN = existing count)

# POST WITH an accountNumber — server overrides it
curl -X POST http://localhost:<port>/api/Accounts \
     -H "Content-Type: application/json" \
     -d '{"accountNumber":"EVIL"}'
# → accountNumber in the response is ACC-10(NN+1), NOT "EVIL"
```

If the second call's response still shows `"accountNumber":"EVIL"`, you're trusting client input for a server-controlled field. Re-read the method and fix it.

---

## Done?

Core passing: you have a working CRUD API over `Account`.
Stretches passing: you have three independent resources and server-controlled account numbers.

Commit, then move on to [Exercise 2](2_scalar_and_http_file.md).

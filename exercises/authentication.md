# Exercise 9 — Authentication: Identity, JWTs, and per-user accounts

**Reference lessons:**
- [`../lessons/authentication/auth_1_identity_and_jwt_bearer.md`](../lessons/authentication/auth_1_identity_and_jwt_bearer.md)
- [`../lessons/authentication/auth_2_register_and_login.md`](../lessons/authentication/auth_2_register_and_login.md)
- [`../lessons/authentication/auth_3_per_user_data.md`](../lessons/authentication/auth_3_per_user_data.md)

**Prerequisites:** anything from Exercise 6 onwards is enough — this exercise does not depend on Exercises 7 (query params) or 8 (DTOs), so you can pick it up immediately after 6 if you want to learn auth before those.

By the end: the bank has user accounts. Customers register, log in, and see only **their** accounts and transactions. An anonymous request is told nothing.

---

## Read this first

This single exercise covers the **three auth lessons** end to end. It's the longest exercise in the course — budget half a day. Three parts, each ending in an acceptance check:

- **Part A** — Identity + JwtBearer wiring + protect one controller (auth_1)
- **Part B** — register + login endpoints that issue real JWTs (auth_2)
- **Part C** — `OwnerId` on accounts, per-user scoping, seed two users (auth_3)

Don't skip Part A's acceptance check before moving on; debugging ownership scoping is much harder if validation is misconfigured.

The Banking domain mapping you'll be working with:

| Lesson concept | Bank equivalent |
|---|---|
| Each `TodoList` belongs to a user | Each `Account` belongs to a user (customer) |
| `Tag.OwnerId` (per-user vocabulary) | We don't have a separate per-user entity — **Account is the only user-owned entity**; transactions inherit ownership through their account |
| Each user gets a default `Inbox` (non-deletable) | Each user gets a starter checking account, account number `ACC-USER<n>-CHK` |

If you've also done stretch goals adding `Branch` or `Customer` entities — those stay global (a branch isn't owned by one user). Only `Account` becomes user-scoped.

---

# PART A — Identity + JwtBearer wiring

**Reference:** [`auth_1_identity_and_jwt_bearer.md`](../lessons/authentication/auth_1_identity_and_jwt_bearer.md)

## Core

### 9.A.1 Install the packages

```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

Two packages. No CLI tool installs — Identity ships with the SDK.

### 9.A.2 Create `ApplicationUser`

Create `Models/ApplicationUser.cs`. Inherit from `IdentityUser`. Empty body is fine — that gives you `Id` (string GUID), `UserName`, `Email`, `PasswordHash`, and the rest of Identity's fields.

### 9.A.3 Change `AppDbContext` to inherit from `IdentityDbContext<ApplicationUser>`

Two-line change:

- Change the base class.
- **Call `base.OnModelCreating(modelBuilder)` as the first line of `OnModelCreating`.** If you forget, you'll get a runtime error about missing `AspNetUsers` on the first query — this is the most common mistake in the whole exercise.

You do **not** need to remove or rewrite anything else in the DbContext. Identity adds seven tables alongside your existing ones; your code keeps working.

### 9.A.4 Add the `Jwt` config section to `appsettings.json`

```json
"Jwt": {
  "Issuer": "banking-api",
  "Audience": "banking-api-clients",
  "SigningKey": "dev-only-signing-key-please-replace-in-production-xxxxxxxxxxxxxxxx",
  "ExpiryMinutes": 60
}
```

The `SigningKey` must be **at least 32 bytes (32 ASCII characters)** for HS256 — anything shorter and the app refuses to start.

### 9.A.5 Wire `Program.cs`

Three service blocks, in this order. The lesson markdown spells the exact code; here's the shopping list:

1. `AddIdentityCore<ApplicationUser>(...)` with relaxed password rules (so demo passwords work) and `RequireUniqueEmail = true`. Chain `.AddEntityFrameworkStores<AppDbContext>()`.
2. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` configured with `TokenValidationParameters` reading `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` from config. Set `NameClaimType = ClaimTypes.NameIdentifier`.
3. `AddAuthorization()`.

Then in the request pipeline (between `UseHttpsRedirection` and `MapControllers`):

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

**The order is load-bearing.** `UseAuthentication` first, `UseAuthorization` second, `MapControllers` third. Swap any two and auth silently breaks.

### 9.A.6 Protect `TransactionsController` with `[Authorize]`

Add `[Authorize]` at the class level on `TransactionsController` only. Leave `AccountsController` open for now — the contrast between protected and open is the demo. Lesson 3 (Part C below) locks down everything.

### Acceptance check

Run the app. Two requests:

```bash
# AccountsController is still open — anonymous works
curl http://localhost:<port>/api/Accounts
# → 200 OK

# TransactionsController is protected — anonymous gets 401
curl -i http://localhost:<port>/api/Transactions
# → HTTP/1.1 401 Unauthorized
```

That's all you should see in Part A. We don't have a way to issue tokens yet — that's Part B. The 401 is the demo: it proves JwtBearer is reading the `Authorization` header (finding it absent), populating `HttpContext.User` with an unauthenticated principal, and `[Authorize]` is short-circuiting the request before your action runs.

If `GET /api/Transactions` returns a 200 instead of 401, something is wrong: either you forgot `[Authorize]`, or the middleware isn't wired in the pipeline, or the order is wrong.

If the app fails to start with `IDX10653`, your `SigningKey` is too short.

If you get a runtime error about missing `AspNetUsers`, your `OnModelCreating` is missing the `base.OnModelCreating(modelBuilder)` call.

---

# PART B — Register and login that issue JWTs

**Reference:** [`auth_2_register_and_login.md`](../lessons/authentication/auth_2_register_and_login.md)

## Core

### 9.B.1 Create the `JwtTokenGenerator` service

Create `Services/JwtTokenGenerator.cs`. It should:

- Take `IConfiguration` in the constructor.
- Expose one public method: `(string token, DateTime expiresAt) Generate(ApplicationUser user)`.
- Build claims for `sub` (user.Id), `email` (user.Email), and `jti` (a fresh `Guid.NewGuid()`).
- Sign with `SymmetricSecurityKey` from `Jwt:SigningKey` and `SecurityAlgorithms.HmacSha256`.
- Set issuer, audience, and expiry from config.
- Return the serialised token via `JwtSecurityTokenHandler().WriteToken(...)`.

Register it as a Singleton in `Program.cs`:

```csharp
builder.Services.AddSingleton<JwtTokenGenerator>();
```

### 9.B.2 Create the auth DTOs

Three records, under a new `Dtos/` folder (you don't have one yet — this is the first time the bank uses request DTOs):

- `RegisterRequest(string Email, string Password)` with `[Required] [EmailAddress]` on email and `[Required] [StringLength(100, MinimumLength = 6)]` on password.
- `LoginRequest(string Email, string Password)` with `[Required] [EmailAddress]` on email and `[Required]` on password — **no length constraint on login passwords**, see the lesson for why.
- `AuthResponse(string AccessToken, DateTime ExpiresAt)` — what register and login both return.

These three DTOs are auth-only. The rest of the API still returns entities directly; introducing response DTOs across the whole bank is exercise 8's job, not this one.

### 9.B.3 Create `AuthController`

Add `Controllers/AuthController.cs` with `[ApiController]` and `[Route("api/auth")]` (note the lowercase explicit route — auth is a verb, not a resource, so we don't use `[controller]`). **No `[Authorize]` at the class level** — these endpoints have to be anonymous.

Constructor takes `UserManager<ApplicationUser>` and `JwtTokenGenerator`.

Two actions:

**`POST /api/auth/register`:**
- Pre-check `FindByEmailAsync` — if a user with this email exists, return `Problem(detail: ..., statusCode: 409)`.
- Create a new `ApplicationUser` with `UserName = email, Email = email`.
- Call `_userManager.CreateAsync(user, request.Password)`.
- If the result is not `Succeeded`, funnel each `result.Errors` entry into `ModelState` and return `ValidationProblem(ModelState)`.
- On success, generate a token and return an `AuthResponse`.

**`POST /api/auth/login`:**
- `FindByEmailAsync`. If the user is null → `Problem(detail: "Invalid email or password.", statusCode: 401)`.
- `CheckPasswordAsync`. If false → return the **same** 401 with the **same** message.
- On success, generate and return an `AuthResponse`.

**The identical 401 on both login failures is non-negotiable.** Returning different responses for "unknown email" vs "wrong password" lets attackers enumerate which addresses are registered. Same status, same body, same trace shape.

### Acceptance check

Run the app, then:

```bash
# Register a customer
TOKEN=$(curl -s -X POST http://localhost:<port>/api/auth/register \
        -H "Content-Type: application/json" \
        -d '{"email":"customer1@example.com","password":"Passw0rd!"}' \
        | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

# The token unlocks the protected controller
curl -H "Authorization: Bearer $TOKEN" http://localhost:<port>/api/Transactions
# → 200 OK with the seeded transactions

# Re-register the same email → 409 Conflict
curl -i -X POST http://localhost:<port>/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"email":"customer1@example.com","password":"Passw0rd!"}'

# Login with wrong password → 401
curl -i -X POST http://localhost:<port>/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"customer1@example.com","password":"wrong"}'

# Login with unknown email → SAME 401, same body shape
curl -i -X POST http://localhost:<port>/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"ghost@example.com","password":"x"}'
```

Five things must be true:

- Register returns `200 OK` with `accessToken` and `expiresAt`.
- That token works on `GET /api/Transactions` (now 200, was 401 in Part A).
- Re-registering the same email → 409.
- Wrong password → 401 with `"Invalid email or password."`
- Unknown email → 401 with **byte-for-byte identical** body. Open both responses in two terminals and verify.

If you decode the token at `jwt.io` you should see `sub` matching the user's Id, `email`, `jti` as a GUID, and your configured `iss` / `aud` / `exp`.

---

# PART C — Per-user accounts and transactions

**Reference:** [`auth_3_per_user_data.md`](../lessons/authentication/auth_3_per_user_data.md)

This is the part where the bank stops being a free-for-all. Each user can only see their own accounts; their accounts only show their own transactions; cross-user lookups return 404, never 403.

## Core

### 9.C.1 Add `OwnerId` to `Account`

Add two members:

```csharp
public string OwnerId { get; set; } = string.Empty;

[JsonIgnore]
public ApplicationUser? Owner { get; set; }
```

`OwnerId` is `string`, not `int` — `IdentityUser.Id` is a GUID string. The two columns must match type for the FK to wire up.

`Transaction` does **not** get an `OwnerId`. Transactions inherit ownership through their parent `Account` — same pattern as `Todo` inheriting through `TodoList` in the lesson.

### 9.C.2 Configure the FK and cascade in `AppDbContext`

Inside `OnModelCreating` (after `base.OnModelCreating`):

```csharp
modelBuilder.Entity<Account>()
    .HasOne(a => a.Owner)
    .WithMany()
    .HasForeignKey(a => a.OwnerId)
    .OnDelete(DeleteBehavior.Cascade);
```

Cascade on delete: removing a user removes their accounts, and (by the existing cascade from Exercise 5) their transactions.

### 9.C.3 Move the seeded accounts out of `HasData` — and seed two users instead

The Inbox-equivalent for the bank: every seeded user gets a starter checking account.

Why this changes: `HasData` rows must be compile-time constants, but `OwnerId` depends on a runtime-generated user `Id`. So the existing `HasData` accounts in `AppDbContext.OnModelCreating` move out, into `DbSeeder`. Same pattern as the lesson moving the Inbox.

Update the seeder:

- It becomes `async Task SeedAsync(...)` (because `UserManager` is async).
- It takes a third parameter: `UserManager<ApplicationUser> userManager`.
- Idempotency: bail if `await userManager.FindByEmailAsync("alice@example.com")` already exists.
- Create two users: `alice@example.com` and `bob@example.com`, password `Passw0rd!` for both.
- For **each** user, create one starter checking account with account number `ACC-{userId-prefix}-CHK` and a `1000m` Credit transaction labelled "Opening deposit". Set `OwnerId` on every account.

Update `Program.cs` to await the new seeder and pass the resolved `UserManager`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await DbSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
}
```

(Top-level statements support `await` natively — no `async Task Main` rewrite needed.)

### 9.C.4 Lock down `AccountsController` and scope every query

Add `[Authorize]` at the class level. Add a private helper:

```csharp
private string GetUserId() =>
    User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated request without a user id claim.");
```

Then update every action:

- **`GET` (list)** — start the query with `.Where(a => a.OwnerId == userId)`. Don't filter after loading; the `Where` goes into the IQueryable so EF translates it to SQL.
- **`GET` (single by id)** — the existence check and the ownership check are **one query**: `FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == userId)`. If it returns null → return `NotFound()`. **404, not 403.**
- **`POST`** — set `account.OwnerId = userId` server-side. Whatever the client put in the request body for `OwnerId` is overwritten.
- **`PUT`** — load the existing row scoped to the current user (single query). 404 if it doesn't exist or isn't theirs. Preserve `OwnerId` on the incoming entity (set to the current user's id) so a malicious client can't reparent.
- **`DELETE`** — same single-query pattern: `FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == userId)`. 404 if not theirs.

### 9.C.5 Lock down `TransactionsController` (ownership inherits)

Already has `[Authorize]` from Part A. Add the `GetUserId()` helper, then scope every query through the parent account:

```csharp
.Where(t => t.Account!.OwnerId == userId)
```

EF Core translates this to a SQL JOIN — the database does the filtering, no in-memory scan.

For `POST /api/Transactions`, before saving, verify the request body's `AccountId` belongs to the current user. If not, return a 400 with a useful detail message (don't reveal whether the account exists at all — the message "AccountId does not exist or does not belong to you" is honest and non-leaky).

### Acceptance check

Run the app. Login both users:

```bash
ALICE=$(curl -s -X POST http://localhost:<port>/api/auth/login \
        -H "Content-Type: application/json" \
        -d '{"email":"alice@example.com","password":"Passw0rd!"}' \
        | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

BOB=$(curl -s -X POST http://localhost:<port>/api/auth/login \
      -H "Content-Type: application/json" \
      -d '{"email":"bob@example.com","password":"Passw0rd!"}' \
      | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
```

Then run all five of these. Every line must produce the marked outcome:

```bash
# 1) Alice sees only Alice's accounts
curl -H "Authorization: Bearer $ALICE" http://localhost:<port>/api/Accounts
# → JSON array containing exactly Alice's starter account

# 2) Bob sees only Bob's accounts
curl -H "Authorization: Bearer $BOB" http://localhost:<port>/api/Accounts
# → JSON array containing exactly Bob's starter account, with a different Id

# 3) Alice tries to GET Bob's account by id → 404 (NOT 403)
# (Replace <bob-account-id> with the id from response 2)
curl -i -H "Authorization: Bearer $ALICE" http://localhost:<port>/api/Accounts/<bob-account-id>
# → HTTP 404 Not Found

# 4) Alice's transactions show only her opening deposit
curl -H "Authorization: Bearer $ALICE" http://localhost:<port>/api/Transactions
# → JSON array with exactly one transaction, the £1000 opening deposit on her account

# 5) Anonymous request → 401
curl -i http://localhost:<port>/api/Accounts
# → HTTP 401
```

If check 3 returns 403 instead of 404, you've leaked existence information — fix the query so the cross-user case returns null and you return `NotFound()`.

If checks 1 and 2 return the same accounts, the `.Where(OwnerId == userId)` isn't being applied. Trace through whether you actually call `GetUserId()` and use the result.

If check 4 returns Bob's transactions too, your scoping on `TransactionsController` isn't filtering through `t.Account!.OwnerId` — re-read the lesson's "Scoping every query" section.

---

## Stretch

### 9.S1 — Per-user transaction creation

**Goal:** when Alice POSTs a new transaction with `AccountId = <one of Bob's account ids>`, return a 400 with a non-leaky detail message.

#### Acceptance

```bash
curl -i -X POST http://localhost:<port>/api/Transactions \
     -H "Authorization: Bearer $ALICE" \
     -H "Content-Type: application/json" \
     -d '{"type":0,"amount":50,"description":"hack","accountId":<BOB_ACCOUNT_ID>}'
# → HTTP 400 with detail "AccountId does not exist or does not belong to you."
```

The transaction is **not** created. Verify by listing as Bob — his account history is unchanged.

### 9.S2 — Account number uniqueness per user

(Easier if you've already done a seeder pattern — this is similar.)

By default, account numbers are globally unique (because of the `ACC-NNNN` autonumber). After this change, the bank still enforces uniqueness, but **per user**: Alice and Bob can both have `ACC-CHK-1` if they each pick that number. Add a composite unique index:

```csharp
modelBuilder.Entity<Account>()
    .HasIndex(a => new { a.OwnerId, a.AccountNumber })
    .IsUnique();
```

Adjust the seeder so each user's starter account uses a deterministic number that's only unique within their own list.

#### Acceptance

Two users can both have an account named `ACC-CHK-1`. Within one user, attempting to POST a duplicate account number returns a 400 (or whatever your conflict shape is — be consistent with the rest of the API).

### 9.S3 — `Customer` entity becomes per-user (if you did 1.S1 / 4.S2 / 5.S2)

If your bank has a `Customer` entity, give it `OwnerId` and scope its controller the same way. Note: `Branch` should stay global — it represents physical infrastructure, not user-owned data. Decide which entities map to which side of the public/private split.

---

## Done?

The bank now has user accounts. Customers register. Each customer's data is invisible to every other customer — including invisible enough that they can't even prove it exists. Anonymous requests get nothing.

If you want to keep going, the natural follow-ups (none of which are in this course as exercises yet):

- **Refresh tokens** — short-lived access tokens + long-lived refresh tokens with proper rotation and revocation.
- **Roles** — `[Authorize(Roles = "BankAdmin")]` on a customer-listing endpoint that you only let admins hit.
- **EF Core global query filters** — DRY the `.Where(OwnerId == userId)` clauses by configuring a query filter once on the DbContext. Read about `HasQueryFilter` and `IHttpContextAccessor`.
- **Rate limit `/api/auth/login`** — JWTs alone don't prevent brute force. Real apps need this.

Commit, breathe, and consider yourself someone who can ship a real authenticated Web API.

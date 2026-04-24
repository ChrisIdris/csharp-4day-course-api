# Teacher's checklist — authentication track

A scannable live-delivery guide for the three auth lessons. Keep it open on a second monitor. Each lesson block has: pre-flight, what to demo in order, gotchas to pre-empt, mid-lesson checkpoints, and a closing test.

---

## Before the first auth lesson (one-time setup)

- [ ] Confirm students have **Visual Studio 17.8+** (or VS Code + C# Dev Kit, or Rider). `dotnet user-jwts` and Scalar bearer UI both need this.
- [ ] Confirm `dotnet --list-sdks` shows **.NET 8 or later**.
- [ ] Students have the **ExtraLesson3** project running (`dotnet run`) and can hit `http://localhost:5107/api/TodoLists` in Scalar / curl. If any student can't get to this point, fix before starting auth.
- [ ] Have `jwt.io` open in a pinned tab — you'll demo token decoding in Lesson 2.
- [ ] Decide how students will get the code: clone the repo or copy `ExtraLesson3 → AuthLesson1` themselves? Live-typing the scaffolding steps is slow; consider pre-provisioning the folder rename + namespace rename and starting students on the empty `Models/ApplicationUser.cs`.
- [ ] Know where your `appsettings.Development.json` lives. Students will edit `appsettings.json` for the signing key; production teams would use secrets. Flag this verbally when you get there.

---

## Auth Lesson 1 — Identity + JwtBearer wiring

### Pre-flight (2 min)

- [ ] Copy `ExtraLesson3 → lessons/authentication/AuthLesson1` and rename csproj/namespaces, or have it pre-done on the projector machine.
- [ ] Close any previously-running Scalar/curl windows — stale sessions confuse the demo.

### The narrative beat

Start with the problem in one sentence: **"Right now anyone on the network can read, create, or delete everything. Let's lock the door."**

Then split the problem visibly into **two halves** on the whiteboard:
1. **Identity** — where users live (a table, password hashes).
2. **Bearer token validation** — middleware that reads `Authorization: Bearer ...` on every request.

**Set scope upfront:** today wires up the *validation* half (the lock and the check). Issuing tokens (the keys) is next lesson. So today's payoff isn't a successful authenticated request — it's a clean **401 on a protected endpoint**, which is exactly what an empty-handed caller should get.

### Demo sequence — in this order

1. [ ] **Install packages** (30 sec each, 1 min total):
   ```bash
   dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
   dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
   ```
2. [ ] **Create `Models/ApplicationUser.cs`**. Show it's empty — inherits everything from `IdentityUser`. Don't list the inherited fields; mention them.
3. [ ] **Change `AppDbContext` base class** from `DbContext` to `IdentityDbContext<ApplicationUser>`. **⚠ Teach the `base.OnModelCreating` gotcha here** — this is the single most common student mistake.
4. [ ] **Add the `Jwt` section to `appsettings.json`**. Walk through each of the four values (Issuer, Audience, SigningKey, ExpiryMinutes). For SigningKey, type a short key deliberately, show the startup error, then fix it to a proper length. **This is the cleanest single moment to teach key-length requirements.**
5. [ ] **Wire `Program.cs`** — three service blocks + two middleware lines. Go in this order, explain each:
   - [ ] `AddIdentityCore` with relaxed password options + `RequireUniqueEmail = true`
   - [ ] `AddAuthentication(JwtBearerDefaults...)` + `AddJwtBearer(...)` — explain each Validate* flag
   - [ ] `AddAuthorization()`
   - [ ] `UseAuthentication()` then `UseAuthorization()` then `MapControllers()` — **draw an arrow on the whiteboard; the order is load-bearing**
6. [ ] **Add `[Authorize]` to `TagsController` only.** Other controllers stay open for now — the contrast is the demo.

### The "401 is the demo" moment

This is the entire payoff for Lesson 1. Two requests, side by side:

- [ ] **Open endpoint, anonymous → 200.** From curl or Scalar:
  - **curl:** `curl http://localhost:5107/api/TodoLists`
  - **Scalar:** click `GET /api/TodoLists` → **Send Request**
- [ ] **Protected endpoint, anonymous → 401.**
  - **curl:** `curl -i http://localhost:5107/api/Tags`
  - **Scalar:** click `GET /api/Tags` → **Send Request**

Walk the class through what just happened on the protected request:
1. JwtBearer middleware looked at the header — no token present
2. It populated `HttpContext.User` with an unauthenticated principal
3. `[Authorize]` saw "not authenticated," short-circuited with 401
4. Our action never ran

**That's the whole pipeline doing its job.** Lesson 2 closes the loop: register/login produces a valid token signed with our key, JwtBearer validates it, `[Authorize]` lets the request through. Today's demo is the door's lock; next lesson's is the key.

If either response is wrong, stop and debug. Most likely cause: middleware ordering, or `base.OnModelCreating` skipped.

### When a student asks "what about `dotnet user-jwts`?"

Some students will know about the built-in dev-token tool. Have a one-liner ready:

> "It's real and it works in projects that use the default config-binding. Our setup is explicit — we configure `IssuerSigningKey` from `Jwt:SigningKey` ourselves so you can see exactly what's being validated. The trade-off is that user-jwts tokens (signed with its own key) get a 401 from our validator. We'll get a real working token in Lesson 2 from `/api/auth/login`."

If the question doesn't come up, don't volunteer it — the 401-is-the-demo framing is cleaner.

### Gotchas to pre-empt

- **⚠ "I called `base.OnModelCreating` but the app still errors."** Check they called it *first*, before any of their custom config.
- **⚠ "My SigningKey is too short"** — `IDX10653` error at startup. HMAC-SHA256 requires ≥32 bytes.
- **⚠ "`[Authorize]` does nothing."** Middleware ordering. `UseAuthentication()` *before* `UseAuthorization()`.
- **⚠ "Doesn't `dotnet user-jwts` issue valid tokens?"** It does, but with its own signing key, not the one we configured. Our explicit `TokenValidationParameters` only trust `Jwt:SigningKey`, so user-jwts tokens fail. Lesson 2's `/api/auth/login` produces the first token our validator accepts.
- **⚠ The NameClaimType line looks weird.** Tell them you'll use it in Lesson 3 when we read the user id; for now, just configure it.

### Closing test (out loud, together)

"What just happened when I hit `/api/Tags` without a token?" — walk through the pipeline:
1. JwtBearer middleware looked for an `Authorization: Bearer` header — none present
2. It set `HttpContext.User` to an unauthenticated principal
3. `[Authorize]` checked `User.Identity.IsAuthenticated` — false → 401
4. Our action never ran

Then: **"Next lesson, the `Authorization: Bearer` header arrives, and we'll watch the same pipeline say yes."**

---

## Auth Lesson 2 — Register/Login + JwtTokenGenerator

### Pre-flight (1 min)

- [ ] Copy `AuthLesson1 → AuthLesson2`, rename. Show the diff command or paste pre-done.
- [ ] Keep Scalar open; they'll see new `/api/auth/` endpoints appear live.

### The narrative beat

**"Last lesson we could validate tokens but not issue them. This lesson fixes that with two endpoints: register and login."**

Immediately address the "can VS scaffold this?" question — many students will ask. Brief answer:
- API Controller (Empty) = skeleton only, saves creating the file
- Identity scaffolder = wrong tool, generates Razor Pages for cookie auth
- `MapIdentityApi<T>()` = works but hides the learning and doesn't issue JWTs

"We write it by hand for the teaching value."

### Demo sequence

1. [ ] **Create `Services/JwtTokenGenerator.cs`** in three highlighted sections:
   - [ ] Signing key + credentials (HS256)
   - [ ] Claims — **show which three we pick and why** (sub/email/jti). Don't rush this; it's the conceptual heart.
   - [ ] Assemble + serialize via `JwtSecurityTokenHandler`
2. [ ] **Register it** in `Program.cs` as Singleton — one line.
3. [ ] **Create the three DTOs** under `Dtos/`:
   - [ ] `RegisterRequest` — `[Required] [EmailAddress]` + `[Required] [StringLength(100, MinimumLength = 6)]`
   - [ ] `LoginRequest` — note: no StringLength on Password. **Teach why.**
   - [ ] `AuthResponse` — two fields
4. [ ] **Create `AuthController`** — write it in this order (don't copy-paste the final version in one go):
   - [ ] Class + `[ApiController]` + `[Route("api/auth")]`. **Point out: no `[Authorize]` on the class.**
   - [ ] Constructor with `UserManager<ApplicationUser>` and `JwtTokenGenerator`.
   - [ ] `Register` — the pre-check, `CreateAsync`, error funnelling, token issuance, auto-login return.
   - [ ] `Login` — the two-step verification. **Pause on the identical 401** — the single most important security point in this lesson.

### The three moments students must internalise

- **`UserManager` is the API.** They never touch `PasswordHash` directly.
- **Identical 401 on both login failure paths.** Draw what user enumeration looks like on the whiteboard: attacker sends `admin@company.com` + garbage password, notes which addresses give "unknown email" vs "wrong password" — now they have the user list. Our identical response prevents that.
- **Auto-login on register is a UX convenience, not a security requirement.** Email-verification flows would change this.

### Mid-lesson checkpoint (pick one path, demo all three)

**Path A — Scalar:**
- [ ] POST `/api/auth/register` with `{"email":"alice@example.com","password":"Passw0rd!"}` → 200 with accessToken
- [ ] Copy the token → paste in Scalar's Auth panel
- [ ] GET `/api/Tags` → 200

**Path B — `.http` file:** Demo the `# @name register` trick + `@token = {{register.response.body.accessToken}}` so the captured token auto-flows into the next request. **Even students who won't use it will go "oh nice."**

**Path C — curl:** For completeness, show one curl to anchor for command-line users. Don't dwell.

**Visual Studio power tip to drop during Path B:** **View → Other Windows → Endpoints Explorer → right-click any endpoint → Generate Request.** This is a feature most students don't know exists.

### Closing demo — the security moments

- [ ] Register `alice@example.com` → 200 with token
- [ ] Register the same email again → **409 Conflict** (the pre-check firing)
- [ ] Login with wrong password → **401, identical body**
- [ ] Login with unknown email → **401, identical body** (user enumeration protection demo)
- [ ] Open `jwt.io`, paste the token, show sub/email/jti/exp/iss/aud. **Highlight: "JWTs are not encrypted. Don't put secrets in claims."**

### Gotchas to pre-empt

- **⚠ "`ValidationProblem` returns weird JSON."** Show the `errors: { Password: [...] }` shape; same as Extra Lesson 2's data-annotation failures. Consistency is the point.
- **⚠ "Why Singleton for the generator?"** It's stateless. Same rule as Slugifier. Stateful services would be Scoped.
- **⚠ Students try to put passwords in claims.** Stop this in its tracks. Only non-sensitive identity facts.

---

## Auth Lesson 3 — Per-user data with OwnerId

### Pre-flight (2 min)

- [ ] Copy `AuthLesson2 → AuthLesson3`, rename.
- [ ] **Warn students this lesson is the biggest diff of the three.** Five files change; don't panic.
- [ ] Keep Scalar open, with Alice's token already set from Lesson 2 — or be ready to log in again after restart.

### The narrative beat

**"Authentication is done. Now we make the data personal. Every list and every tag gains an owner. Every query filters by the current user's id. Alice cannot see — cannot even detect the existence of — Bob's data."**

### The five file changes — in this order

1. [ ] **`Models/TodoList.cs` + `Models/Tag.cs`** — add `OwnerId` (string, matches `IdentityUser.Id`) and `[JsonIgnore] ApplicationUser? Owner`. **Teach: `string` not `int`, because Identity uses GUID ids. Also: why `Todo` doesn't need OwnerId — inherits through `TodoList`.**
2. [ ] **`Data/AppDbContext.cs`** — three modifications:
   - [ ] Change slug indexes to composite `(OwnerId, Slug)` with `.IsUnique()`. **Teach: "Alice and Bob can both have a `groceries` slug."**
   - [ ] Add `HasOne(...).WithMany().HasForeignKey(...).OnDelete(Cascade)` for both entities.
   - [ ] **Remove the `HasData` Inbox.** Explain why: OwnerId isn't a compile-time constant, so the Inbox moves to `DbSeeder`. **This is the clearest moment in the course to contrast HasData vs imperative seeding.**
3. [ ] **`Data/DbSeeder.cs`** — becomes `SeedAsync`, takes `UserManager`. Seed alice + bob, then call `SeedUserDataAsync(...)` for each. **Show that each user gets their own Inbox.**
4. [ ] **`Program.cs`** — update the scope block to resolve `UserManager` and `await DbSeeder.SeedAsync(...)`. Entry point becomes `async Task Main` equivalent (top-level statements handle this automatically).
5. [ ] **The three data controllers** — `TodoListsController`, `TagsController`, `TodosController`:
   - [ ] Add `[Authorize]` at the class level
   - [ ] Add the `GetUserId()` helper — read from `ClaimTypes.NameIdentifier`. **Reference Lesson 1 where we set `NameClaimType`.**
   - [ ] Every GET gets `.Where(x => x.OwnerId == userId)` — **teach: it goes INTO the query, not after**
   - [ ] Every POST sets `OwnerId = userId` **after** mapping from the request — server is authoritative
   - [ ] Every PUT/DELETE uses single-query existence+ownership check with 404-not-403
   - [ ] Slug helper gains an `ownerId` parameter — per-user scope

### The three moments students must internalise

- **404, not 403.** Draw the enumeration attack on the whiteboard: if Alice gets 403 for Bob's id=7 but 404 for id=9999, she knows id=7 exists for *someone*. 404 everywhere hides existence.
- **The server is authoritative on OwnerId.** Overwrite whatever the client sent. If you ever teach multi-user apps and only remember one thing, remember this.
- **Why Todo has no OwnerId.** Ownership inherits through the parent. Query filters by `t.TodoList.OwnerId`, EF translates to a JOIN.

### Mid-lesson checkpoint

Run it. Login as Alice and Bob in sequence. Show in Scalar:

```
Alice's /api/TodoLists → ids 2, 3, 4 — her Inbox/Groceries/Work tasks
Bob's   /api/TodoLists → ids 5, 6, 7 — different ids, same titles, different owner
```

### Closing demo — the four security checks

Use the seeded alice/bob accounts. Login both in separate tabs / save two tokens.

- [ ] **Isolation** — Alice's GET returns only Alice's lists. Bob's returns only Bob's.
- [ ] **Cross-user 404** — Alice GETs one of Bob's ids → 404 (not 403).
- [ ] **Cross-user DELETE 404** — Alice DELETEs one of Bob's ids → 404. Bob's list still exists afterwards (Bob confirms via his own GET).
- [ ] **Per-user slug collision** — Alice POSTs a second "Groceries" → slug becomes `groceries-2`. Bob's `groceries` is untouched.
- [ ] **Inbox still non-deletable** — Alice tries to DELETE her Inbox → 400 (Deletable guard from main course still works, proving per-lesson business rules compose).

### Gotchas to pre-empt

- **⚠ "My `await` at top-level won't compile."** The `using (var scope = ...)` block is now `await DbSeeder.SeedAsync(...)`. Top-level statements support `await` natively — no signature change needed.
- **⚠ "Seeder throws on startup."** Usually the password doesn't meet the Identity rules from Lesson 1 — check `RequiredLength = 6` and no digit/upper/lower requirement, or pick a password that meets them.
- **⚠ "`.Where(t => t.TodoList.OwnerId == userId)` gives a null warning."** Use `t.TodoList!.OwnerId == userId` with the null-forgiving operator — safe because EF translates to SQL, it's not a runtime dereference.
- **⚠ Students forget to set `OwnerId` on POST.** The row inserts but with empty-string OwnerId; it shows up for nobody. Test by creating and then listing.
- **⚠ The asked-for "can I avoid the `.Where` duplication?" question** — answer briefly (EF Core global query filters) and defer full treatment to a future lesson.

### After the lesson — the "next steps" teaser

Natural follow-ups to mention but not teach:
- Refresh tokens + revocation
- Global query filters to DRY the `.Where(OwnerId == ...)`
- Policy-based auth (`[Authorize(Policy = "...")]`)
- Rate limiting on `/api/auth/login`

---

## Live-demo data cheat sheet

Paste-ready requests for when you need to move fast:

```json
// Register
{"email":"alice@example.com","password":"Passw0rd!"}

// Login (wrong password, demo enumeration protection)
{"email":"alice@example.com","password":"wrong"}

// Login (unknown email — identical 401 response)
{"email":"ghost@example.com","password":"anything"}
```

Seeded users in AuthLesson3:
- `alice@example.com` / `Passw0rd!`
- `bob@example.com` / `Passw0rd!`

---

## If you only have time for ONE demo in each lesson

- **Lesson 1:** Scalar side-by-side: `GET /api/TodoLists` → 200, `GET /api/Tags` → 401. Same server, same client, two outcomes — that's the entire `[Authorize]` pipeline in two clicks.
- **Lesson 2:** The identical 401 on wrong-password and unknown-email. Teach enumeration in 60 seconds with nothing but two curls.
- **Lesson 3:** Alice and Bob side-by-side in two tabs. Alice GETs id=7 (Bob's) → 404. Same list is visible in Bob's tab. One screen, two facts.

# Auth Lesson 2 — Registering accounts and issuing JWTs

> Start from the end of **Auth Lesson 1**. Copy `AuthLesson1/` to `AuthLesson2/` and rename the project.

Lesson 1 wired up the middleware that validates incoming tokens. What's missing is a way for clients to *get* a token in the first place. Two HTTP endpoints are standard:

- `POST /api/auth/register` — create a new account, hash the password, return a token.
- `POST /api/auth/login` — verify email and password, return a token.

Both endpoints return the same shape — an access token and its expiry — so the client can register once and use the token immediately, or log in on return visits and get a fresh one.

## The JWT-issuing service

A JWT has three parts joined by dots: `header.payload.signature`. The header announces the algorithm (`HS256`). The payload is a JSON object of **claims** — small facts about the user. The signature is an HMAC of the first two parts computed with the shared signing key.

`JwtSecurityTokenHandler` from the BCL does all three steps for us, but we wrap it in our own service so controllers don't care about the details. `Services/JwtTokenGenerator.cs`:

```csharp
public class JwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public (string token, DateTime expiresAt) Generate(ApplicationUser user)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:SigningKey"]!));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var expires = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
```

The claims are the interesting part.

| Claim | Meaning | Why we include it |
|---|---|---|
| `sub` | Subject — who this token identifies | Controllers read this to know the current user's id |
| `email` | User's email | Convenience, so controllers don't hit the DB for basic info |
| `jti` | JWT ID — unique per token | Lets us add a revocation list later; for now, just a fresh GUID each time |

What travels in every JWT automatically: `iss`, `aud`, `exp`, `iat`, `nbf` — issuer, audience, expiry, issued-at, not-before. JwtSecurityToken sets them from the constructor arguments and timestamps.

Register it in `Program.cs` the same way as Slugifier — stateless + thread-safe = Singleton:

```csharp
builder.Services.AddSingleton<JwtTokenGenerator>();
```

## Symmetric signing and why it matters here

`SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)` uses **symmetric signing**: the same secret that signs the token validates it. That's why `Program.cs`'s JwtBearer config reads the same `Jwt:SigningKey` that this service uses — if they drift, the tokens we issue won't pass validation.

Larger systems with many services use **asymmetric signing** (RS256): one service holds a private key to sign, every other service holds the matching public key to verify. Our API signs and verifies in the same process, so HMAC is the right tool.

## The request and response DTOs

Three records under `Dtos/`. Data-annotation validation from Extra Lesson 2 does the shape-checking:

```csharp
public record RegisterRequest(
    [Required] [EmailAddress] string Email,
    [Required] [StringLength(100, MinimumLength = 6)] string Password
);

public record LoginRequest(
    [Required] [EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(string AccessToken, DateTime ExpiresAt);
```

Notice `LoginRequest.Password` has no `[StringLength]`. The length rule is for *creating* a password, not *verifying* one. A user whose old short password predates a policy change should still be able to log in. And returning a "too short" error on login would leak policy information to attackers probing which accounts exist.

## Can Visual Studio scaffold this for us?

Before we write it, the reasonable question: the earlier lessons scaffolded `TodoListsController` and `TagsController` with `dotnet aspnet-codegenerator controller` — is there an equivalent for an auth controller? The short answer is **no, not one that produces what we want**.

Three nearby options, and why none of them fit:

1. **Right-click Controllers → Add → Controller → API Controller (Empty)** in Visual Studio. This gives you a class with `[ApiController]` and `[Route("api/[controller]")]` and an empty body. Useful as a starting skeleton — saves you the boilerplate of creating the file by hand — but every meaningful line (DI parameters, register action, login action, token issuance) you still write yourself. Same story with `dotnet new apicontroller` on the CLI. **A skeleton, not a scaffold.**

2. **The Identity scaffolder** (Add → New Scaffolded Item → **Identity** in Visual Studio, or `dotnet aspnet-codegenerator identity` on the CLI). This generates a full login/register/forgot-password/two-factor stack — but as **Razor Pages**, with cookie authentication, for server-rendered apps. Totally wrong shape for a JWT-based Web API. Generating it into this project would leave you with ~15 `.cshtml` files to delete.

3. **`MapIdentityApi<TUser>()`** (one line in `Program.cs`, introduced in .NET 8). This is the closest thing to a "zero-code auth" option — it registers ready-made endpoints for `/register`, `/login`, `/refresh`, `/confirmEmail`, and more. Tempting. Two reasons we don't use it:
   - **It issues opaque bearer tokens, not JWTs by default.** The tokens are only valid against the same API's token store; other services can't validate them without a database round-trip. To get proper JWTs you'd replace the entire token provider, which is more code than writing a controller.
   - **It hides every line worth teaching.** Claims, signing keys, password checking, enumeration protection — all of that happens inside the framework, invisible. For production it's a reasonable path. For learning it's the wrong tool.

So: we write the controller. Right-click **Controllers → Add → Controller → API Controller - Empty** if you want the file created for you with the attributes already in place; everything else below you type in.

## The controller

`Controllers/AuthController.cs`:

```csharp
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthController(UserManager<ApplicationUser> userManager, JwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Problem(
                detail: "An account with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(err.Code, err.Description);
            return ValidationProblem(ModelState);
        }

        var (token, expiresAt) = _tokenGenerator.Generate(user);
        return new AuthResponse(token, expiresAt);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var (token, expiresAt) = _tokenGenerator.Generate(user);
        return new AuthResponse(token, expiresAt);
    }
}
```

No `[Authorize]` on the class. These endpoints have to be anonymous — you can't require a token to *get* a token.

## The three parts of the controller that deserve attention

**`UserManager<ApplicationUser>`** is the Identity API for user operations. It came for free with `AddIdentityCore` in Lesson 1; we just ask for it in the constructor. Its three methods we use:

- `FindByEmailAsync(email)` — returns the user or null. Uses the `NormalizedEmail` column so it's case-insensitive.
- `CreateAsync(user, password)` — hashes the password via `PasswordHasher`, validates against the rules we configured in Lesson 1, writes the row. Returns an `IdentityResult` with any errors.
- `CheckPasswordAsync(user, password)` — hashes the provided password and compares against the stored hash in constant time. Never reveals which bytes differed.

We never touch `PasswordHash` or `SecurityStamp` directly. `UserManager` is the whole interface.

**The email-uniqueness check in Register.** `User.RequireUniqueEmail = true` from Lesson 1 means `CreateAsync` would *also* catch a duplicate email with a `DuplicateEmail` error. We pre-check with `FindByEmailAsync` so we can return a cleaner 409 response. Either approach works; this one produces nicer JSON.

**The identical 401 on both login failures.** "Wrong password" and "no such email" both return the same response — same status, same message. That's deliberate. If the API told attackers "no account with that email," they could enumerate which addresses are registered, which helps phishing campaigns and credential-stuffing attacks. Giving both failure modes the same shape prevents the leak.

## Auto-login on register

The Register endpoint returns a token in the body. That means "registering" and "logging in for the first time" are a single round-trip. The client saves the token, makes authenticated requests immediately, no extra login call.

Not every API does this. If you have email verification (user must click a link before the account is usable), register would return a 201 with no token, and login would reject unverified accounts. For a todo app with no email pipeline, the simpler flow is fine.

## The end-to-end test

Pick whichever of the three paths below matches your editor. Each one hits the same endpoints — the `curl` is the canonical reference, but Scalar and the `.http` file are more pleasant day-to-day.

### Path A — Scalar in the browser

Open `http://localhost:5107/scalar/v1`. You'll see `AuthController` in the sidebar alongside the others.

1. Click **POST `/api/auth/register`**. In the request body editor, paste `{"email":"alice@example.com","password":"Passw0rd!"}` and hit **Send Request**.
2. The response panel shows `200 OK` with `accessToken` and `expiresAt`. **Copy the `accessToken` value** (the raw `eyJ...` string).
3. Click the **Authentication** icon in Scalar's top bar, pick **HTTP Bearer**, paste the token (Scalar adds the `Bearer ` prefix itself), save.
4. Click **GET `/api/Tags`** → **Send Request** → `200 OK`. The protected endpoint from Lesson 1 now works.
5. Go back to **POST `/api/auth/register`** and send the same body again → `409 Conflict`.
6. Try **POST `/api/auth/login`** with the wrong password, then with an unknown email. Both return `401 Unauthorized` with the *identical* body.

This is the full end-to-end loop without a single terminal command.

### Path B — the `.http` file in Visual Studio / VS Code / Rider

Append these blocks to `AuthLesson2.http`:

```http
@AuthLesson2_HostAddress = http://localhost:5107

### Register Alice
# @name register
POST {{AuthLesson2_HostAddress}}/api/auth/register
Content-Type: application/json

{
  "email": "alice@example.com",
  "password": "Passw0rd!"
}

### Capture the token from the register response for reuse below
@token = {{register.response.body.accessToken}}

### Access a protected endpoint with the captured token
GET {{AuthLesson2_HostAddress}}/api/Tags
Authorization: Bearer {{token}}

### Login later with the right password
POST {{AuthLesson2_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "alice@example.com",
  "password": "Passw0rd!"
}

### Login with the wrong password — 401
POST {{AuthLesson2_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "alice@example.com",
  "password": "wrong"
}

### Login with an unknown email — SAME 401 response
POST {{AuthLesson2_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "ghost@example.com",
  "password": "whatever"
}
```

The magic line is `@token = {{register.response.body.accessToken}}`. The `# @name register` comment above the first request gives it the name `register`; the variable reference then pulls `accessToken` out of *that* request's response and reuses it in the next block's `Authorization` header. No copy-paste between requests.

**Visual Studio power move.** Use **View → Other Windows → Endpoints Explorer** to see every registered route in a tree. Right-click any endpoint → **Generate Request** creates an `.http` block for it automatically — saves you writing the POST method, URL, and `Content-Type` header by hand.

### Path C — curl on the command line

For completeness, the same sequence in `curl`:

```bash
# Register Alice
curl -X POST http://localhost:5107/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"email":"alice@example.com","password":"Passw0rd!"}'
# → {"accessToken":"eyJ...","expiresAt":"..."}

# Save the token
TOKEN="eyJ..."

# The protected endpoint from Lesson 1 now accepts the token
curl -H "Authorization: Bearer $TOKEN" http://localhost:5107/api/Tags
# → 200 with the tags

# Register the same email again
curl -i -X POST http://localhost:5107/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"email":"alice@example.com","password":"Passw0rd!"}'
# → HTTP 409 — An account with this email already exists.

# Login later with the right password
curl -X POST http://localhost:5107/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"alice@example.com","password":"Passw0rd!"}'
# → fresh token

# Login with the wrong password
curl -i -X POST http://localhost:5107/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"alice@example.com","password":"wrong"}'
# → HTTP 401 — Invalid email or password.

# Login with an unknown email — SAME response as wrong password
curl -i -X POST http://localhost:5107/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"ghost@example.com","password":"whatever"}'
# → HTTP 401 — Invalid email or password.
```

## Decode the token for yourself

Copy your token, head to `jwt.io`, and paste it in. The site decodes the three parts in front of you — you'll see the `sub` matching the user's Id, `email` matching Alice's address, `jti` as a random GUID, and `iss`/`aud`/`exp`/`iat` matching the config. The signature section is flagged as "verified" if you also paste the signing key. Every piece of the token is legible; it's the *signature* that prevents tampering, not secrecy of the payload.

This is worth internalising — **JWTs are not encrypted.** Anyone who intercepts one reads every claim. Don't put secrets in claims. Ever.

## Where this leads

Register and login work. `TagsController` is protected. Hit any other endpoint (`/api/TodoLists`, `/api/Todos`) and you still see everyone's data — we haven't separated users yet.

**Auth Lesson 3** adds an `OwnerId` column to `TodoList` and `Tag`, protects every data controller with `[Authorize]`, and makes every query filter by the current user's Id from the token's `sub` claim. Alice sees only Alice's data; Bob sees only Bob's. The seed data grows into two users, each with their own Inbox.

# Auth Lesson 1 — Wiring up Identity and JWT bearer authentication

> Start from the end of **Extra Lesson 3**. Copy `extra_lessons/ExtraLesson3/` to `lessons/authentication/AuthLesson1/` and rename the project.

Our API has shipped eight lessons and three extras without a single authenticated request. Anyone who can reach the server can read every list, create tags, delete todos. That was fine for teaching the mechanics — it's exactly what you can't ship to real users.

This lesson puts the **authentication infrastructure** in place. By the end, one endpoint will reject unauthenticated calls with a 401. What it won't do yet is give anyone a way to get a valid token — that's Lesson 2. For testing, we'll use a built-in .NET tool that can forge dev tokens.

## Two sides of the problem

When people say "add authentication," they usually mean two separate things that have to be built together:

- **Identity** — where user accounts live. Who exists, what their password hash is, what email they use. This is a database problem, and Microsoft ships a first-party answer called **ASP.NET Core Identity** that we'll use.
- **Bearer token validation** — how the API decides whether an incoming HTTP request is from an authenticated user. Every request carries an `Authorization: Bearer <token>` header; something middleware-shaped reads it, validates the signature, and populates `HttpContext.User`. That something is **`Microsoft.AspNetCore.Authentication.JwtBearer`**.

Identity handles "who are the users." JwtBearer handles "is this request from one of them." We wire both in this lesson; neither issues tokens — that's the next lesson.

## Adding the packages

```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

## The user type

Identity wants a class that represents a row in the users table. The canonical path is to inherit from `IdentityUser`, which already gives you `Id`, `UserName`, `Email`, `PasswordHash`, `SecurityStamp`, lockout fields, and everything else the framework needs to do its job. Add per-user fields (display name, avatar URL) here; leave it empty if you're happy with the defaults.

```csharp
// Models/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace AuthLesson1.Models;

public class ApplicationUser : IdentityUser
{
}
```

`IdentityUser` uses a `string` Id — a GUID by default. Our existing entities use `int` ids and that's fine; they're different tables with different conventions.

## Making `AppDbContext` Identity-aware

The DbContext changes base class: from `DbContext` to `IdentityDbContext<ApplicationUser>`. That single change brings seven new tables into the model — `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserRoles`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`. For a todo app we only ever touch `AspNetUsers`, but the others travel along for when you want roles or external logins later.

```csharp
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ...existing DbSets...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CRITICAL: base.OnModelCreating FIRST. IdentityDbContext configures all
        // its tables inside this override. Skip it and you'll get a runtime error
        // about the missing AspNetUsers table on your first query.
        base.OnModelCreating(modelBuilder);

        // ...existing TodoTag composite PK, slug indexes, HasData...
    }
}
```

The `base.OnModelCreating(modelBuilder)` call is the one thing everyone forgets. Identity needs it to configure its schema; without it, nothing about users works.

## Configuration — where the JWT settings live

Bearer tokens are signed with a secret key. The API issues them with that key, and the middleware validates them with the same key. We need one place where both sides read the value, and that's `appsettings.json`:

```json
"Jwt": {
  "Issuer": "todos-api",
  "Audience": "todos-api-clients",
  "SigningKey": "dev-only-signing-key-please-replace-in-production-xxxxxxxxxxxxxxxx",
  "ExpiryMinutes": 60
}
```

Four values. Each has a job:

- **`Issuer`** — a string the token claims as its originator. The middleware checks the token's `iss` claim matches. Totally arbitrary — pick anything; just keep issuer and validator configured to the same value.
- **`Audience`** — who the token was issued to. Same deal: arbitrary string, both sides must agree.
- **`SigningKey`** — the shared secret used to sign and verify. HS256 (what we'll use) requires at least **256 bits** — that's 32 ASCII characters. Use a longer one in practice. **Never commit the production key to git.** Put it in `appsettings.Development.json` for local dev and a secret store for production.
- **`ExpiryMinutes`** — how long a token stays valid. Short-lived access tokens (15-60 min) paired with refresh tokens is the standard pattern; refresh tokens are a later lesson.

## Wiring it in `Program.cs`

Three blocks, in order. Before `var app = builder.Build();`:

```csharp
// 1) Identity: user store + UserManager + PasswordHasher.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Dev-friendly password rules — relax for teaching, tighten for prod.
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredLength = 6;

        // Enforce unique emails — Identity doesn't do this by default.
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

// 2) JwtBearer: validate incoming tokens on every request.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

// 3) Authorization services ([Authorize] attribute machinery).
builder.Services.AddAuthorization();
```

A few things deserve unpacking.

**`AddIdentityCore` vs `AddIdentity`.** They look similar. `AddIdentity` registers cookie authentication as a side effect, which fights with our JWT-only setup. `AddIdentityCore` is the lighter cousin — it gives us `UserManager` and the password-hashing infrastructure without adding a cookie scheme. For a JWT API, this is the right call.

**`User.RequireUniqueEmail = true`.** By default Identity only requires unique *usernames*. Since our register flow uses email as the username and as the login identifier, we want the email column unique too. This is how you enforce the "no two accounts with the same email" rule at the storage layer.

**The four `Validate*` flags on `TokenValidationParameters`.** These are what the middleware actually checks on every incoming token:

| Flag | What the middleware does when true |
|---|---|
| `ValidateIssuer` | Reject if `iss` claim doesn't match `ValidIssuer` |
| `ValidateAudience` | Reject if `aud` claim doesn't match `ValidAudience` |
| `ValidateLifetime` | Reject if the token has expired or isn't yet valid |
| `ValidateIssuerSigningKey` | Reject if the signature doesn't verify against `IssuerSigningKey` |

All four default to `true` when you set `IssuerSigningKey` — but making them explicit signals intent.

**`NameClaimType = ClaimTypes.NameIdentifier`.** This one's a foot-gun. .NET silently remaps the `sub` claim (the standard JWT field for "who is this for") to a weird XML-schema URL by default, so `User.FindFirstValue(ClaimTypes.NameIdentifier)` returns `null` in controllers. Pinning `NameClaimType` here turns `sub` into `NameIdentifier`, and we get the idiomatic `User.FindFirstValue(ClaimTypes.NameIdentifier)` for free.

## The middleware pipeline

Two new lines between `UseHttpsRedirection()` and `MapControllers()`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

Order matters. Each piece depends on the previous:

- `UseAuthentication()` reads the `Authorization` header, validates the token, and populates `HttpContext.User` with a `ClaimsPrincipal` carrying the token's claims.
- `UseAuthorization()` enforces `[Authorize]` attributes against that `ClaimsPrincipal`. If there's no authenticated user, it short-circuits with a 401 before the controller action runs.
- `MapControllers()` dispatches the request to the matched action.

Swap any two of them and auth silently breaks — often in confusing ways. `[Authorize]` becomes a no-op, or unauthenticated requests reach your controller expecting a populated `User`. Memorise this ordering.

## The smoke test — locking down one controller

Add `[Authorize]` to the top of `TagsController`:

```csharp
using Microsoft.AspNetCore.Authorization;

[Authorize]                             // NEW
[Route("api/[controller]")]
[ApiController]
public class TagsController : ControllerBase { ... }
```

Leave the other controllers alone for now. Lesson 3 locks down everything; for this lesson, one protected controller is enough to see the pipeline working.

## Testing the pipeline — confirming the 401

This lesson wired up the *validation* half of the auth pipeline. The *issuing* half — actually producing a valid token — comes in Lesson 2 when we build `/api/auth/login`. So the goal of testing right now isn't to see a successful authenticated request; it's to confirm that:

- Anonymous requests to **unprotected** endpoints still go through (`/api/TodoLists` → 200).
- Anonymous requests to **protected** endpoints get rejected (`/api/Tags` → 401).

That's the whole pipeline doing its job. JwtBearer middleware looked at the request, found no `Authorization` header, populated `HttpContext.User` with an unauthenticated principal, and `[Authorize]` short-circuited the request before the action ran.

### From curl

```bash
dotnet run

# Open endpoint — no token needed
curl http://localhost:5107/api/TodoLists
# → 200 OK, JSON array

# Protected endpoint — no token, no access
curl -i http://localhost:5107/api/Tags
# → HTTP/1.1 401 Unauthorized
```

### From Scalar

Open `http://localhost:5107/scalar/v1` in the browser.

1. Click `GET /api/TodoLists` → **Send Request** → 200, the lists come back.
2. Click `GET /api/Tags` → **Send Request** → 401.

That contrast — same server, same client, same code path, two different outcomes purely because of `[Authorize]` — is what you want to feel. Lesson 2 then completes the round trip.

### Aside — what about `dotnet user-jwts`?

If you've seen .NET tutorials, you may know about `dotnet user-jwts create` — a CLI tool that issues development tokens. It exists, it works, and it's reasonable in projects that bind their JwtBearer config to ASP.NET Core's defaults (i.e. `.AddJwtBearer()` with no explicit `TokenValidationParameters`).

We're *not* using it here, on purpose. The tool signs its tokens with a key it stores in user-secrets, not the `Jwt:SigningKey` we put in `appsettings.json`. Our `Program.cs` configures `IssuerSigningKey` explicitly from our config — partly because it's a clearer teaching shape, partly because most production setups configure validation explicitly too. The cost is that user-jwts tokens get a 401 from our middleware. The benefit is that students see exactly which key and which issuer/audience are validated.

Issuing a real token that our validator accepts is the job of Lesson 2's `JwtTokenGenerator`, which uses the same `Jwt:SigningKey` for signing that JwtBearer uses for validation. That's where the "200 with a valid token" demo lives.

## The HS256 signing key length

If you shorten the `SigningKey` below 32 bytes, the app refuses to start with a runtime exception. HMAC-SHA256 requires a key at least as long as the hash output (256 bits = 32 bytes). Any shorter and the guarantees don't hold.

For production keys: generate them with a cryptographically random source, not by typing at the keyboard. `openssl rand -base64 48` is a reasonable one-liner. Rotate them through a secret manager (Azure Key Vault, AWS Secrets Manager, etc.) — never commit them to source control.

## What we've built, what we haven't

The app now has:

- A user table (`AspNetUsers`) ready to accept accounts.
- The JwtBearer middleware validating every incoming token against a known issuer, audience, lifetime, and signing key.
- One controller (`TagsController`) protected by `[Authorize]` to demonstrate the validation side rejects anonymous requests.

It has no way to issue tokens of its own yet — anonymous-only is the demo until Lesson 2. **Auth Lesson 2** introduces a proper `AuthController` with register + login endpoints and the `JwtTokenGenerator` service that signs JWTs with the same key our validator trusts. That closes the loop: register → token → authenticated request → 200.

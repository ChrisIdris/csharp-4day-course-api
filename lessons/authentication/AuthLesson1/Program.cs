using System.Security.Claims;
using System.Text;
using AuthLesson1.Data;
using AuthLesson1.Models;
using AuthLesson1.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));

builder.Services.AddSingleton<Slugifier>();

// -----------------------------------------------------------------------------
// NEW in Auth Lesson 1 — three blocks, in this order:
//   1) AddIdentityCore : user store + UserManager + PasswordHasher
//   2) AddAuthentication + AddJwtBearer : how incoming tokens get validated
//   3) AddAuthorization : the counterpart that [Authorize] reads from
// -----------------------------------------------------------------------------

// 1) AddIdentityCore (not AddIdentity): we want UserManager and password hashing
// but NOT the cookie auth scheme that AddIdentity would also add. For a JWT-only
// API, cookies would confuse the default-scheme resolution.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Dev-friendly password rules. Production apps crank these up.
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredLength = 6;

        // Enforce unique emails — Identity doesn't do this by default.
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

// 2) JwtBearer: the middleware that reads the Authorization header on every
// incoming request and parses the token into a ClaimsPrincipal on HttpContext.User.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Four things every validator checks:
            ValidateIssuer = true,              // who issued this token
            ValidateAudience = true,            // who's it intended for
            ValidateLifetime = true,            // not expired
            ValidateIssuerSigningKey = true,    // signature matches our key

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),

            // .NET silently remaps "sub" to a weird long URL claim type by default.
            // Pinning NameClaimType here means User.FindFirstValue(ClaimTypes.NameIdentifier)
            // returns the "sub" value directly — the idiomatic way to get the user id
            // in a controller.
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

// 3) Authorization services ([Authorize] attribute machinery).
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<Slugifier>());
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// MIDDLEWARE ORDER IS LOAD-BEARING:
//   UseAuthentication  → populate HttpContext.User from the bearer token
//   UseAuthorization   → enforce [Authorize] against HttpContext.User
//   MapControllers     → dispatch to actions that see the principal
// Swap any two of these and auth silently breaks.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

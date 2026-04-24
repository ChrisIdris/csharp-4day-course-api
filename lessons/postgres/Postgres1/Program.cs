using Postgres1.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// NEW — read the connection string named "AppDb" from configuration.
// Configuration sources are layered: appsettings.json → appsettings.{Environment}.json
// → environment variables → user-secrets (in dev). The app doesn't care which source
// supplies the value — it just asks for the string named "AppDb". That layering is
// what lets dev use a committed local connection string while prod uses an env var
// without either side knowing about the other.
// Throwing on a missing value makes configuration bugs fail loudly at startup, not
// silently at the first query.
var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Connection string 'AppDb' is not configured.");

// NEW — swap the in-memory provider for Npgsql (the PostgreSQL provider for EF Core).
// No other part of the app changes: DbContext, migrations, LINQ queries, controllers
// — all provider-agnostic. This is the whole payoff of EF Core as an abstraction.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Seed the database on startup.
// DbContext is a "scoped" service (one per HTTP request), so we can't resolve it
// directly from app.Services (which is the root / singleton container). We open a
// scope, resolve the context from within it, run the seeder, then dispose the scope.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // NEW — TEMPORARY. EnsureCreated creates the schema from the current model if the
    // DB is empty, and does NOTHING if the DB already has tables. That's fine for this
    // lesson (we have no migrations yet), but it's a dead end: it ignores migrations
    // entirely, and it can't evolve an existing schema. Lesson 2 replaces this line
    // with db.Database.Migrate().
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Publishes the OpenAPI document at /openapi/v1.json.
    app.MapOpenApi();

    // Mounts the Scalar UI at /scalar/v1. Scalar reads the OpenAPI document
    // produced above and renders an interactive API explorer in the browser.
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

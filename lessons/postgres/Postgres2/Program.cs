using Postgres2.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Connection string 'AppDb' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Seed the database on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // NEW — Migrate() replaces EnsureCreated(). Behavioural difference matters:
    //
    //   EnsureCreated() — "if no DB exists, create one from the current model."
    //                     Ignores migrations entirely. No-op once tables exist.
    //   Migrate()       — "apply every migration in Migrations/ that hasn't been
    //                     applied yet." Uses __EFMigrationsHistory to know which.
    //                     Works on a fresh DB AND on a live one.
    //
    // Running BOTH is a bug: EnsureCreated would create tables outside the migration
    // history, then Migrate would fail trying to create them again.
    db.Database.Migrate();
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

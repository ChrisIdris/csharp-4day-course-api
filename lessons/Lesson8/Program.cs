using Lesson8.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the EF Core DbContext with the in-memory provider.
// The string "TodoDb" is just the name of the in-memory database instance.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));

var app = builder.Build();

// Seed the in-memory database on startup.
// DbContext is a "scoped" service (one per HTTP request), so we can't resolve it
// directly from app.Services (which is the root / singleton container). We open a
// scope, resolve the context from within it, run the seeder, then dispose the scope.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // EnsureCreated is a no-op for an in-memory database that's already initialized,
    // but it's good habit — with a real provider it would create the schema.
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

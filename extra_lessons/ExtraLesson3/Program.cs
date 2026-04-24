using ExtraLesson3.Data;
using ExtraLesson3.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));

// New in Extra Lesson 3 — register Slugifier with the DI container as a Singleton.
// Slugifier is stateless (no fields that change per request) and thread-safe, so one
// instance shared by the entire app is fine. Controllers ask for a Slugifier in their
// constructor and the DI container hands them the single instance.
builder.Services.AddSingleton<Slugifier>();

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

app.UseAuthorization();

app.MapControllers();

app.Run();

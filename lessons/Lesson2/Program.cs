using Lesson2.Data;
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

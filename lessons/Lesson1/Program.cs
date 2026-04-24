using Lesson1.Data;
using Microsoft.EntityFrameworkCore;

// "Top-level statements" (C# 9+): no `Main` method needed — this file *is* the entry point.
// `WebApplication.CreateBuilder(args)` sets up the host: configuration sources (appsettings.json,
// environment variables, command-line args), logging, and the dependency injection (DI) container.
var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------
// 1) Register services in the DI container (builder.Services)
// -----------------------------------------------------------
// Anything registered here can later be injected into controllers, middleware, etc. via constructor
// parameters. This phase is *configuration only* — nothing is running yet.

// Registers MVC controller services and discovers classes decorated with [ApiController].
// Without this, your [Route]/[HttpGet] attributes wouldn't be picked up.
builder.Services.AddControllers();

// Registers OpenAPI document generation — used by tooling (Swagger UI, Scalar, .http files) and IDEs.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the EF Core DbContext with the in-memory provider.
// `AddDbContext<AppDbContext>` registers AppDbContext as a *scoped* service, meaning each HTTP
// request gets its own fresh instance (important — DbContext is not thread-safe).
// The lambda builds the `DbContextOptions<AppDbContext>` that AppDbContext's constructor receives.
// "TodoDb" is the logical name of the in-memory database; any context using the same name sees the
// same data. In-memory is great for learning and tests; in a real app you'd swap for e.g.
// `UseSqlServer(...)`, `UseSqlite(...)`, or `UseNpgsql(...)` — only this line needs to change.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));

// Build the actual WebApplication from the configured builder. After this point we configure the
// *HTTP request pipeline* rather than services — the two phases are deliberately separate.
var app = builder.Build();

// -----------------------------------------------------------
// 2) Configure the HTTP request pipeline (middleware)
// -----------------------------------------------------------
// Middleware runs in the order it's added. Each piece can either handle the request itself or pass
// it along to the next piece. Order matters — e.g. HTTPS redirect should come before auth.

// Only expose the OpenAPI document in development — you usually don't want to publish your API
// schema in production. `app.Environment` reads from the ASPNETCORE_ENVIRONMENT variable.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redirect plain http:// requests to https:// — a safe default for any real web API.
app.UseHttpsRedirection();

// Enables [Authorize] attributes to actually do something. We're not using auth yet, but wiring it
// up now means adding it later is a one-line change in a controller.
app.UseAuthorization();

// Route incoming requests to controller actions based on [Route]/[HttpGet]/etc. attributes.
// This is where all the controllers registered by `AddControllers()` become reachable URLs.
app.MapControllers();

// Start the Kestrel server and block until shutdown (Ctrl+C, SIGTERM, etc.).
app.Run();

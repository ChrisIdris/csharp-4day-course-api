# Lesson 1 — Your first Web API: project, model, DbContext, scaffolded controller

By the end of this lesson you will have a running ASP.NET Core Web API that exposes full CRUD for a single `TodoList` resource, backed by an in-memory Entity Framework Core database.

**What we will do, in order:**

1. Create a new Web API project using the `dotnet` CLI.
2. Clean out the template's sample (`WeatherForecast`).
3. Install the Entity Framework Core and scaffolding NuGet packages.
4. Write the `TodoList` model.
5. Write the `AppDbContext` and register it as a service.
6. Scaffold a full CRUD controller with one CLI command.
7. Run the API and hit it from the terminal.

---

## 1. Create the project

From this `lessons/` folder, run:

```bash
dotnet new webapi --use-controllers -n Lesson1 -o Lesson1
cd Lesson1
```

A few things to notice:

- `--use-controllers` tells the template to generate a **controller-based** Web API (the alternative is Minimal APIs, which put routes directly in `Program.cs`). For this course we use controllers because they scale better as the app grows and because the scaffolder we use in step 6 produces a controller class.
- `-n Lesson1` sets both the project name and default namespace.
- `-o Lesson1` puts the project in a subfolder called `Lesson1`.

Take a minute to look around:

```
Lesson1/
├── Controllers/
│   └── WeatherForecastController.cs   ← sample controller (we'll delete this)
├── Properties/launchSettings.json     ← dev server URLs/ports
├── appsettings.json                   ← config (logging, connection strings, …)
├── Lesson1.csproj                     ← project file: target framework + NuGet refs
├── Lesson1.http                       ← editor-runnable HTTP requests (more on this in Lesson 2)
├── Program.cs                         ← the app's entry point
└── WeatherForecast.cs                 ← sample model
```

### `Program.cs` — the entry point

Open `Program.cs`. This is where every ASP.NET Core app starts. Even though it looks like a script, it **is** the `Main` method — C# 10's top-level statements hide the boilerplate. The important stages are:

```csharp
var builder = WebApplication.CreateBuilder(args);     // (1) configure services

builder.Services.AddControllers();                    // (2) register services (the "DI container")
builder.Services.AddOpenApi();

var app = builder.Build();                            // (3) build the app

app.MapControllers();                                 // (4) configure the HTTP pipeline

app.Run();                                            // (5) start the web server
```

Everything we do in this lesson is either a new entry in stage (2) or a new file that a controller will use.

---

## 2. Remove the sample

We're replacing `WeatherForecast` with `TodoList`, so delete the sample files:

```bash
rm WeatherForecast.cs Controllers/WeatherForecastController.cs
```

---

## 3. Install the EF Core and scaffolding packages

We'll use **Entity Framework Core** (EF Core) as our ORM and its **in-memory provider** so we don't need to install a real database. We also need the scaffolding tooling for step 6.

```bash
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.VisualStudio.Web.CodeGeneration.Design
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

> **Wait, why SqlServer if we're using InMemory?** This is a known quirk of the `aspnet-codegenerator` tool we'll use in step 6 — it refuses to generate a controller unless the SqlServer package is referenced, even though the generated code is provider-agnostic. Once it's in the csproj, forget about it; nothing at runtime touches SqlServer.

After running these commands, open `Lesson1.csproj` — you'll see the new `<PackageReference>` entries.

### Install the scaffolding CLI tool (one-time per machine)

```bash
dotnet tool install -g dotnet-aspnet-codegenerator
```

If you already installed an older version and hit an error in step 6, update it:

```bash
dotnet tool update -g dotnet-aspnet-codegenerator
```

---

## 4. The `TodoList` model

Make a `Models/` folder and a file `Models/TodoList.cs`:

```csharp
namespace Lesson1.Models;

public class TodoList
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Soft "is-deletable" flag. The DELETE action in the controller will check this
    // before removing a row. Default `true` — only special lists (e.g. a user's Inbox,
    // which we seed in Lesson 3) are marked false.
    public bool Deletable { get; set; } = true;
}
```

Things worth calling out:

- **`Id`** — EF Core treats a property named exactly `Id` (or `<ClassName>Id`) as the primary key by convention; no attributes needed.
- **`string Title { get; set; } = string.Empty;`** defaults to `""`. In modern C# with nullable reference types enabled, a non-nullable `string` property must be initialized, or the compiler warns.
- **`string? Description`** — the `?` means "this may be null." That's how we mark an optional field.
- **`Deletable`** — a plain `bool` with a default of `true`. We'll use it in step 6 to block accidental (or malicious) deletion of lists that shouldn't be deleted.

---

## 5. The DbContext

Make a `Data/` folder and a file `Data/AppDbContext.cs`:

```csharp
using Lesson1.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson1.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
}
```

**What's going on here:**

- A **`DbContext`** is EF Core's representation of a database session. You query it, you change tracked entities on it, you call `SaveChangesAsync()` on it.
- The constructor takes `DbContextOptions<AppDbContext>`. This is how ASP.NET's DI container hands us our configuration (connection string / provider / etc.).
- Each **`DbSet<T>`** corresponds to a table. `TodoLists` maps to a `TodoLists` table (EF pluralises automatically). `=> Set<TodoList>()` is a terser way to write `{ get { return Set<TodoList>(); } }`.

### Register the DbContext in `Program.cs`

Open `Program.cs` and wire it up:

```csharp
using Lesson1.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// NEW — register AppDbContext with the in-memory provider.
// "TodoDb" is just a name for this in-memory database instance.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

`AddDbContext<T>` is the service registration. Now anywhere in the app we can ask for an `AppDbContext` in a constructor and DI will deliver one scoped to the current HTTP request.

---

## 6. Scaffold the controller

This is the magic step. Run from the `Lesson1/` folder:

```bash
dotnet aspnet-codegenerator controller \
  -name TodoListsController \
  -async \
  -api \
  -m Lesson1.Models.TodoList \
  -dc Lesson1.Data.AppDbContext \
  -outDir Controllers
```

Each flag means:

- `-name TodoListsController` — class name.
- `-async` — generate `async` / `await` action methods.
- `-api` — Web API controller (no views).
- `-m Lesson1.Models.TodoList` — the **model** the controller works with.
- `-dc Lesson1.Data.AppDbContext` — the **DbContext** the controller talks to.
- `-outDir Controllers` — where to put the file.

You should see:

```
Using database provider 'Microsoft.EntityFrameworkCore.InMemory'!
Added Controller : '/Controllers/TodoListsController.cs'.
```

Open `Controllers/TodoListsController.cs`. You'll get a full CRUD controller with:

| Action | HTTP | Route |
|---|---|---|
| `GetTodoLists` | GET | `/api/TodoLists` |
| `GetTodoList` | GET | `/api/TodoLists/{id}` |
| `PostTodoList` | POST | `/api/TodoLists` |
| `PutTodoList` | PUT | `/api/TodoLists/{id}` |
| `DeleteTodoList` | DELETE | `/api/TodoLists/{id}` |

We've added **inline comments** throughout explaining each attribute and action — read through the file slowly. The headline concepts:

- `[Route("api/[controller]")]` — `[controller]` is replaced by `TodoLists` (class name minus "Controller"), producing `/api/TodoLists`.
- `[ApiController]` — turns on the Web API conventions (automatic 400 on model-binding failure, etc.).
- **Constructor injection** — the controller's constructor asks for an `AppDbContext`, which DI supplies because we registered it in step 5.
- **`ActionResult<T>`** — the return type means "either an HTTP status-code result (`NotFound()`, `BadRequest()`, …) or a `T` that ASP.NET will serialize to JSON."
- **`CreatedAtAction`** — the standard way to respond to a successful POST: returns `201 Created` with a `Location` header pointing at the new resource.

---

## 6b. Respect the `Deletable` flag

The scaffolder doesn't know about our `Deletable` property — so `DeleteTodoList` will happily remove anything you POST. That's not what we want for protected rows like an Inbox list. Edit the `DeleteTodoList` action to check the flag:

```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteTodoList(int id)
{
    var todoList = await _context.TodoLists.FindAsync(id);
    if (todoList == null)
    {
        return NotFound();
    }

    // NEW — business rule: some lists cannot be deleted.
    if (!todoList.Deletable)
    {
        return Problem(
            detail: "This list is marked as non-deletable and cannot be removed.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    _context.TodoLists.Remove(todoList);
    await _context.SaveChangesAsync();
    return NoContent();
}
```

**What's `Problem()` doing?** It returns a response that follows the [RFC 7807 "problem details" standard](https://tools.ietf.org/html/rfc7807) — a widely-used JSON shape for returning errors from HTTP APIs. ASP.NET's `[ApiController]` attribute turns this into:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "This list is marked as non-deletable and cannot be removed.",
  "traceId": "00-..."
}
```

Much friendlier than a bare `400` — clients can programmatically read `detail` for the reason, and `traceId` helps you cross-reference your server logs.

**Why 400 instead of 403 Forbidden?** 403 would imply an authorisation failure ("you're not allowed to do this"). Here the problem is with the *resource itself* — no user could delete it, regardless of identity. 400 is the honest answer. Both choices are defensible; what matters is being consistent across the API.

For now every row comes in with `Deletable = true`, so you won't see the 400 yet. In **Lesson 3** we'll seed an Inbox list with `Deletable = false` and exercise this guard.

---

## 7. Run it

```bash
dotnet run
```

Expected output (port may differ slightly):

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5107
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

In a second terminal:

```bash
# List everything — should be empty on a fresh start
curl http://localhost:5107/api/TodoLists
# → []

# Create one
curl -X POST http://localhost:5107/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Demo","description":"Hello"}'
# → {"id":1,"title":"Demo","description":"Hello","createdAt":"...","deletable":true}

# Fetch it back
curl http://localhost:5107/api/TodoLists/1

# Delete it — succeeds because deletable is true by default
curl -X DELETE http://localhost:5107/api/TodoLists/1
# → 204 No Content

# Try to create a non-deletable list and then delete it
curl -X POST http://localhost:5107/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Protected","deletable":false}'
# → {"id":2,...,"deletable":false}

curl -i -X DELETE http://localhost:5107/api/TodoLists/2
# → HTTP/1.1 400 Bad Request
# → {"type":"...","title":"Bad Request","status":400,
#    "detail":"This list is marked as non-deletable and cannot be removed.",...}
```

Stop the server with `Ctrl+C`.

> **Important:** the in-memory database lives inside the running process. Every time you `dotnet run`, the database starts empty again. We'll fix that in **Lesson 3** by seeding it.

---

## Recap

You now have a working Web API that:

- Uses a **model** (`TodoList`) and an **EF Core DbContext** (`AppDbContext`).
- Has the DbContext **registered as a service** and **injected** into controllers.
- Exposes a full set of CRUD endpoints, generated by the scaffolder rather than written by hand.
- Enforces a simple business rule (`Deletable`) in the DELETE action, using RFC-7807 problem details.

In **Lesson 2** we'll make the API easier to explore by adding the Scalar UI and writing a `.http` file with sample requests.

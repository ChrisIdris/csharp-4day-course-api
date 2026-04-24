# Lesson 2 — Explore your API: Scalar UI and the `.http` file

In Lesson 1 we built a CRUD API but had to test it with `curl`. Typing requests into a terminal is fine for one or two endpoints, but it gets old fast. In this lesson we add two tools that make exploring the API much more pleasant:

1. **Scalar** — a browser-based API reference that reads the OpenAPI document your app already generates and turns it into a clickable, try-it-in-the-browser UI.
2. **The `.http` file** — a plain-text file your editor treats as runnable HTTP requests. No curl, no Postman.

Start from a copy of your Lesson 1 project (or open `lessons/Lesson2` in this repo).

---

## 1. Install the Scalar package

From the `Lesson2/` folder:

```bash
dotnet add package Scalar.AspNetCore
```

That's the only new NuGet dependency. OpenAPI itself (`Microsoft.AspNetCore.OpenApi`) is already present — it came with the Web API template.

---

## 2. Wire Scalar into `Program.cs`

Add one `using` and one line in the dev-only block:

```csharp
using Lesson2.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;   // NEW

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Publishes the OpenAPI document at /openapi/v1.json
    app.MapOpenApi();

    // NEW — mounts the Scalar UI at /scalar/v1
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

Two things happening here:

- `MapOpenApi()` exposes a machine-readable description of every endpoint (their routes, verbs, request bodies, response shapes) as JSON at `/openapi/v1.json`.
- `MapScalarApiReference()` serves an HTML/JS page at `/scalar/v1` that **reads that JSON** and turns it into a friendly UI.

Both are wrapped in `if (app.Environment.IsDevelopment())` so they aren't exposed in production.

---

## 3. See all your endpoints in the Scalar window

Run the app:

```bash
dotnet run
```

Open your browser to:

```
http://localhost:5107/scalar/v1
```

You should see:

- A **sidebar on the left** listing every controller (grouped by tag). You'll see **TodoLists** with five operations: `GET /api/TodoLists`, `GET /api/TodoLists/{id}`, `POST`, `PUT`, `DELETE`.
- Click any operation and the right-hand pane shows:
  - the HTTP method and URL,
  - the **request body schema** (for POST / PUT), auto-generated from the `TodoList` model,
  - the **response schemas** for each status code,
  - a **"Test Request"** panel you can use to fire the request right from the browser and see the response.

If you add a new endpoint to a controller later, you don't need to configure anything — reload the page and Scalar picks it up automatically.

---

## 4. The `.http` file

Your project has a file called `Lesson2.http`. This is a **Visual Studio / VS Code / JetBrains Rider convention** — when your editor sees a file with the `.http` extension, it parses it as a series of HTTP requests and offers a "Send Request" button (in VS Code it shows up as a CodeLens above each request).

Open `Lesson2.http` and replace its contents with:

```http
@Lesson2_HostAddress = http://localhost:5107

### Get all todo lists
GET {{Lesson2_HostAddress}}/api/TodoLists
Accept: application/json

###

### Create a new todo list
POST {{Lesson2_HostAddress}}/api/TodoLists
Content-Type: application/json

{
  "title": "Weekend chores",
  "description": "Things to do this Saturday"
}

###
```

**Anatomy of this file:**

- `@Lesson2_HostAddress = ...` — a **variable**. You reference it later with `{{Lesson2_HostAddress}}`. If your port changes, update it in one place.
- `###` — the separator between request blocks. Everything between two `###` lines is one request.
- The first line of each block is `<METHOD> <URL>`.
- Then optional **headers** (`Accept: ...`, `Content-Type: ...`), one per line.
- Then a **blank line**, followed by the **request body** (for POST / PUT).

### Sending requests

Make sure your API is running (`dotnet run`), then:

- **VS Code** — install the *REST Client* extension. A "Send Request" link appears above each request.
- **Rider / Visual Studio** — a green "play" icon appears in the gutter.

Click the link on the GET request — a new pane opens with the response (status, headers, body).

Click the link on the POST request — the body goes as JSON, the API creates the todo list, and you should see `HTTP/1.1 201 Created` plus the created object in the response.

Now run the GET again and you'll see the item you just created.

---

## Recap

You now have two ways to explore and exercise your API:

- **Scalar** — best for discovery: "what endpoints exist? what does this response look like?"
- **`.http` file** — best for repeatable manual tests: "send the same POST five times" or "check a specific edge case."

Both stay up to date **automatically** as you add endpoints. No manual doc maintenance.

In **Lesson 3** we'll deal with a nagging problem: every time we restart the app our data is gone. We'll seed the in-memory database so there's always something to look at.

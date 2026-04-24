# Lesson 8 — DTOs: decoupling the wire from the database

Lesson 7 ended at a wall. We wanted `?include=list` on `GET /api/Todos/{id}` to optionally embed the parent TodoList in the response. It didn't work. Back in Lesson 6 we'd put `[JsonIgnore]` on `Todo.TodoList` to break the cycle, and `[JsonIgnore]` is a **compile-time** attribute — it can't be switched on and off per request. The shape of the response is fixed at build time, and the shape we fixed at build time hides the parent.

That's not really a Lesson 7 problem, though — it's a *deeper* problem we've been glossing over. We've been dressing up database entities as our public API. Every `GET /api/TodoLists` sends a JSON object that's a 1:1 reflection of a row in the `TodoLists` table: same fields, same names, same nullability. When you squint, you realise the *database schema* and the *API contract* are two different documents serving two different audiences — the database serves our code's storage needs, the API serves the clients that read and write to it. They shouldn't be the same shape. They just happen to line up for a little while in a small project.

This lesson introduces **DTOs** — *Data Transfer Objects*. Small classes, purpose-built for one direction of the wire. One for responses going out, one for requests coming in. They're what the API promises, and they have nothing to do with how EF Core happens to store things.

> **What we're NOT doing this lesson:** input validation (`[Required]`, `[StringLength]`, etc. — Lesson 9), mapping libraries like AutoMapper, `CreateTodoRequest` / `UpdateTodoRequest` DTOs for the plain POST/PUT endpoints (those arrive with validation). One concept at a time — today it's DTOs.

Open your Lesson 7 project, copy it to Lesson 8, or open `lessons/Lesson8` in this repo.

---

## 1. What a DTO actually is

A DTO is nothing more than a class whose job is to carry data between layers of your system. In our case, between the controller and the client on the other side of the HTTP request. It has no behaviour, no persistence, no relationships — it's a **shape**, and that's it.

You'll write two kinds:

- **Response DTOs** — what your API sends *out*. You decide the shape based on what the client needs (not what the database stores).
- **Request DTOs** — what your API accepts *in*. You decide the shape based on the operation the client is performing (not what the entity looks like).

Three reasons DTOs exist:

1. **Decoupling.** Your database schema and your API contract can evolve independently. Add a column to the entity without breaking a single client; rename a JSON field without touching the database.
2. **Hiding internal fields.** Not everything in the database belongs on the wire. Passwords, internal flags, audit metadata — DTOs let you hide them at the boundary instead of trusting every controller to remember to strip them.
3. **Operation-shape requests.** A client that wants to "create a todo AND its new parent list in one call" is doing one logical *operation*, but no single entity describes the input. DTOs let the request body match the operation, not the entity.

---

## 2. Quick aside — `record` vs `class` vs `struct`

Before we write our first DTO, a short word on which C# type to use. DTOs are little more than bags of data that flow in and out, and there's a C# feature purpose-built for exactly that: the **record**.

A `record` is, informally, a class where the compiler writes the boring bits for you — value-based equality, a `ToString` that lists every property, support for non-destructive copies via `with { ... }`, and an optional shorthand syntax called a **positional record** that collapses the whole thing to one line.

Here's the shortest possible positional record:

```csharp
public record Point(int X, int Y);
```

That one line gives you: two read-only properties (`X` and `Y`), a constructor that takes both, value-based equality (`new Point(1, 2) == new Point(1, 2)` is `true`), a readable `ToString` (`"Point { X = 1, Y = 2 }"`), and a `Deconstruct` method so you can write `var (x, y) = somePoint`.

For DTOs specifically, records are the right choice because:

- **They're immutable by default** (via `init`-only setters). Once the response is built, nothing can mutate it out from under you.
- **They're concise.** A five-field DTO is five lines instead of twenty.
- **Value-based equality** makes them pleasant in tests — two response DTOs with the same field values are equal, no setup required.

When would you use a `class` instead? When the type has behaviour, lifecycle, or mutable state beyond "hold values." Our entities (`TodoList`, `Todo`) are classes — they have EF Core tracking, they get mutated via PUT, they implement interfaces. A `struct` is for small value types copied by value — too niche for API shapes. A `record struct` exists (a record with value-type semantics), but for DTOs the default `record class` is almost always what you want.

If you've never written a record before, Microsoft has a tutorial walkthrough: **[Use record types tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/tutorials/records)**. It's a short read and covers positional records, `with` expressions, and when to reach for them.

We'll use `record` for every DTO in this lesson.

---

## 3. Response DTOs — shaping the data that goes out

Make a new folder `Dtos/` alongside `Models/`. This is where wire-shaped types live, clearly separated from the database-shaped types in `Models/`.

### The todo's response DTO

```csharp
// Dtos/TodoResponse.cs
namespace Lesson8.Dtos;
using Lesson8.Models;

public record TodoResponse(
    int Id,
    string Title,
    bool IsComplete,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TodoListId
)
{
    public static TodoResponse FromEntity(Todo todo) =>
        new(todo.Id, todo.Title, todo.IsComplete, todo.CreatedAt, todo.UpdatedAt, todo.TodoListId);
}
```

First, look at what's **there**: `Id`, `Title`, `IsComplete`, `CreatedAt`, `UpdatedAt`, `TodoListId`. Every field a client would reasonably need.

Now look at what's **not there**: no `TodoList` navigation back-reference. None at all. It's not that we've hidden it with an attribute — **the DTO simply doesn't have that property**. A cycle can't form through a field that doesn't exist. This is why DTOs are the *root* fix to the Lesson 6 cycle problem; `[JsonIgnore]` was a patch on the entity, but DTOs don't need a patch because there's nothing to patch.

### The `FromEntity` static method

Every DTO in this lesson pairs with a `FromEntity` method that turns the entity into the DTO. Let's look at that line again:

```csharp
public static TodoResponse FromEntity(Todo todo) =>
    new(todo.Id, todo.Title, todo.IsComplete, todo.CreatedAt, todo.UpdatedAt, todo.TodoListId);
```

It's a plain `static` method. Nothing fancy. It takes an entity, reads its fields, and returns a new `TodoResponse` positional-record instance whose constructor arguments map one-to-one to the record's properties. At the call site, the name reads beautifully:

```csharp
TodoResponse.FromEntity(todo);
```

Why pick this approach over alternatives?

- **It's grep-able.** Search for `TodoResponse.FromEntity` and you find every mapping point. No magic.
- **It's a single source of truth.** If we add a field to the entity or the DTO, there's exactly one place to update the mapping.
- **No library dependency.** Mapping libraries like AutoMapper and Mapster exist and do this automatically via reflection, but at the cost of "how does this field map?" becoming opaque. For beginners and small projects, writing the mapping by hand is clearer and faster than learning AutoMapper conventions. You might reach for a library later; you might not.

Placing the method **on the DTO itself** (rather than on the entity, or in a separate `Mapper` class) keeps the mapping rule next to the thing it's producing. Someone reading `TodoResponse.cs` sees *both* the wire shape and the rule that populates it. When the wire shape changes, the mapping rule is right there.

### The list's response DTO

```csharp
// Dtos/TodoListResponse.cs
namespace Lesson8.Dtos;
using Lesson8.Models;

public record TodoListResponse(
    int Id,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<TodoResponse>? Todos
)
{
    public static TodoListResponse FromEntity(TodoList list) =>
        new(
            list.Id,
            list.Title,
            list.Description,
            list.CreatedAt,
            list.UpdatedAt,
            list.Todos?.Select(TodoResponse.FromEntity).ToList()
        );
}
```

Again — pay attention to what's **not there**: `Deletable`.

`Deletable` is a real field on the entity. Our controller uses it to enforce a business rule in `DELETE`. But it's an **internal** flag — not part of what our API promises to the outside world. Clients don't need to see it. If they try to `DELETE` a non-deletable list, they get a `400 Bad Request` with a clear `Problem` response explaining why. That's the public contract. The boolean flag that drives the implementation of that contract doesn't need to be published.

This is a real tradeoff, and reasonable people disagree. Some APIs *do* expose `Deletable` (or a similar `canBeDeleted`) so client UIs can hide the delete button up-front rather than discovering the rule by trial and error. That's a valid choice — the argument is about UX, not purity. The point of this lesson isn't that you should always hide `Deletable`; it's that with DTOs, the decision is *yours to make* rather than whatever the entity happens to declare. When the wire format is a DTO, including or excluding a field is a deliberate API design choice.

### Mapping nested collections

The interesting line is the last argument to the constructor:

```csharp
list.Todos?.Select(TodoResponse.FromEntity).ToList()
```

Three things are happening. First, `list.Todos?.` — if the navigation wasn't eager-loaded (`.Include(l => l.Todos)` wasn't called), `list.Todos` is `null` and the whole expression short-circuits to `null`. The response then has `"todos": null`, preserving the "honest null" behaviour we set up in Lesson 5.

Second, `.Select(TodoResponse.FromEntity)` — if the todos *were* loaded, we map each entity through the same `FromEntity` method we already wrote. Reuse, no special case.

Third, `.ToList()` materialises the result. The `Select` alone returns an `IEnumerable<TodoResponse>`, but our DTO expects a `List<TodoResponse>?` — so we call `ToList()` to produce one.

The composition reads as: "if we have todos, map each of them; otherwise null."

---

## 4. Rewire the controllers to return DTOs

Now we point the controllers at the DTOs. Every action that used to return an entity (or an `IEnumerable<>` of entities) gets updated to return the DTO instead. The action signature changes, the query runs against the entity as before, and the last step projects through `FromEntity`.

In `TodoListsController`, the list endpoint becomes:

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<TodoListResponse>>> GetTodoLists([FromQuery] TodoListQuery query)
{
    var q = _context.TodoLists.AsQueryable();

    if (ParseInclude(query.Include).Contains("todos"))
    {
        if (query.Completed.HasValue)
        {
            bool wantComplete = query.Completed.Value;
            q = q.Include(l => l.Todos!.Where(t => t.IsComplete == wantComplete));
        }
        else
        {
            q = q.Include(l => l.Todos);
        }
    }

    var lists = await q.ToListAsync();
    return lists.Select(TodoListResponse.FromEntity).ToList();
}
```

Compare that to the Lesson 7 version. The entire query-building middle is unchanged — `.AsQueryable()`, conditional `.Include()`, filtered includes. All we did is:

- Change the return type from `IEnumerable<TodoList>` to `IEnumerable<TodoListResponse>`.
- Add a single `.Select(TodoListResponse.FromEntity)` after `.ToListAsync()`.

That's it. The query still talks to EF in terms of entities; the response still talks to the client in terms of DTOs; the projection happens at the seam between them.

`GetTodoList` (single) and `PostTodoList` follow the same pattern. Inside `PostTodoList`, note how `CreatedAtAction` takes the DTO as its body:

```csharp
return CreatedAtAction(
    nameof(GetTodoList),
    new { id = todoList.Id },
    TodoListResponse.FromEntity(todoList));
```

PUT and DELETE don't change — they don't return a response body (both return `204 No Content` or `400 Problem`), so there's nothing to project.

`TodosController` gets the same treatment using `TodoResponse`.

### A nuance on PUT and POST input

PUT and the plain POST still **accept** entities as their request body:

```csharp
public async Task<IActionResult> PutTodoList(int id, TodoList todoList) { ... }
```

We *could* introduce `UpdateTodoListRequest` / `CreateTodoListRequest` DTOs right now — that would be consistent. We're not doing that in this lesson because input DTOs without validation are half a story: the real reason you'd want a request DTO for these endpoints is to say "the client isn't allowed to set `Id`, `CreatedAt`, `UpdatedAt`, or `Deletable` on the way in." That's a validation concern. Lesson 9 introduces validation and input DTOs together, because teaching one without the other would be making a case students can't yet finish.

---

## 5. Request DTOs — shaping the data that comes in

Here's the example where request DTOs *aren't optional*. Imagine the client wants to:

> Create a new TodoList called "Weekend," and inside it, create a todo called "Pack bags." In one call.

What should the request body be? There's no entity shape that fits. It's neither a `TodoList` (which doesn't include a child todo to create) nor a `Todo` (which doesn't include a new parent list). It's a shape that describes the **operation**, which is exactly what DTOs exist for.

```csharp
// Dtos/CreateTodoWithNewListRequest.cs
namespace Lesson8.Dtos;

public record CreateTodoWithNewListRequest(
    string TodoTitle,
    bool IsComplete,
    string ListTitle,
    string? ListDescription
);
```

Four fields. Two describe the todo, two describe its new parent list. The record has no `FromEntity` method because it's not going to be mapped *from* anything — it's the body of an incoming request, so the only direction it travels is in.

### The endpoint

Add this action to `TodosController`:

```csharp
[HttpPost("with-new-list")]
public async Task<ActionResult<TodoResponse>> CreateWithNewList(CreateTodoWithNewListRequest request)
{
    var list = new TodoList
    {
        Title = request.ListTitle,
        Description = request.ListDescription
    };
    _context.TodoLists.Add(list);
    await _context.SaveChangesAsync();

    var todo = new Todo
    {
        Title = request.TodoTitle,
        IsComplete = request.IsComplete,
        TodoListId = list.Id
    };
    _context.Todos.Add(todo);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, TodoResponse.FromEntity(todo));
}
```

The route is `POST /api/Todos/with-new-list` — a **sub-route** under `TodosController`. When an operation doesn't fit the simple REST pattern of "one resource, five verbs," a named sub-route is the standard solution. The URL reads as the intent ("post a todo, with a new list").

ASP.NET automatically binds the JSON request body to the `CreateTodoWithNewListRequest` parameter because `[ApiController]` infers that the complex-type parameter comes from the body. No attribute needed.

### Two `SaveChanges` calls — why?

You'll notice the action saves *twice*. This is exactly the two-phase pattern we used in Lesson 5's `DbSeeder`, and for exactly the same reason. The todo needs `TodoListId = list.Id` — but `list.Id` is 0 until EF actually saves the list and the database assigns an Id. So we save the list first, let EF populate `list.Id`, then use that Id when building the Todo.

If one of these calls fails (say the Todo insert fails for some reason), the list has already been committed — you've got a brand-new empty list sitting around. That's a transactionality problem that a real database connection would solve with `BeginTransaction()` / `CommitAsync()`. The in-memory provider doesn't truly support transactions, and teaching `DbContext` transaction APIs cleanly deserves its own lesson. For now we live with the limitation — it's a known thing, not a hidden bug.

### What the response looks like

`CreatedAtAction` produces a `201 Created`, sets a `Location` header pointing at `GET /api/Todos/{newTodoId}`, and returns the newly-created todo as a `TodoResponse`. The client gets back:

```json
{
  "id": 6,
  "title": "Pack bags",
  "isComplete": false,
  "createdAt": "2026-04-24T18:00:48.305795Z",
  "updatedAt": null,
  "todoListId": 5
}
```

The `todoListId: 5` is how the client knows which list was created. If they want the full list, they can `GET /api/TodoLists/5`.

---

## 6. About that `[JsonIgnore]` on `Todo.TodoList` — keep it?

Strictly speaking: no, we don't need it any more. Every endpoint in this lesson returns a DTO, and DTOs don't have a `TodoList` back-reference, so the serializer never encounters a cycle.

We're keeping it anyway, as belt-and-braces. The argument is: a future endpoint might be added that accidentally returns an entity directly — say, a quick admin endpoint someone hacks together — and if the attribute is there, that endpoint's response is still cycle-safe. The cost of the attribute is one line; the cost of rediscovering the cycle bug after it ships to prod is much higher. This is the same reasoning as "defence in depth" in security — multiple independent safeguards are cheaper than trusting a single one.

The lesson in `Models/Todo.cs` updates the comment on `[JsonIgnore]` to reflect this: it's now a safety net, not the load-bearing fix.

---

## 7. Try it

Run the app and exercise the new shapes:

```bash
dotnet run
```

```bash
# 1) List response — notice "deletable" is absent from every list
curl "http://localhost:5107/api/TodoLists" | python3 -m json.tool

# 2) Single list with todos — nested TodoResponse objects, no "todoList" back-reference on each todo
curl "http://localhost:5107/api/TodoLists/1?include=todos" | python3 -m json.tool

# 3) Compound POST — create a list and its first todo in one call
curl -X POST "http://localhost:5107/api/Todos/with-new-list" \
     -H "Content-Type: application/json" \
     -d '{"todoTitle":"Pack bags","isComplete":false,"listTitle":"Weekend","listDescription":"Weekend plans"}'

# 4) Confirm the compound operation worked
curl "http://localhost:5107/api/TodoLists/5?include=todos" | python3 -m json.tool
```

The list responses have no `deletable` field anywhere. The todo responses have no `todoList` field anywhere. The compound POST returns `201 Created` and populates both entities atomically-enough for the in-memory provider.

---

## Where this leads

We've moved the wire shape out of the database's hands. Response DTOs publish exactly what the API promises; request DTOs describe operations the client is performing. The cycle problem that needed a band-aid in Lesson 6 is now solved at the root — DTOs don't have the back-reference, so there's nothing to cycle through.

But the inbound side is still loose. `POST /api/Todos` accepts a whole `Todo` entity — meaning a client could set `Id`, `CreatedAt`, or `UpdatedAt` on the way in, and we'd happily persist whatever they sent. We also have no rules: `Title` could be empty, or 10,000 characters, or `null`. The API doesn't enforce anything yet.

That's **Lesson 9**. We'll introduce input DTOs (`CreateTodoRequest`, `UpdateTodoRequest`), data-annotation validation attributes (`[Required]`, `[StringLength]`, `[Range]`), and the `[ApiController]` automatic `400 Bad Request` that kicks in when the model binding fails. When we're done, the API won't just *shape* its inputs — it'll *enforce* them.

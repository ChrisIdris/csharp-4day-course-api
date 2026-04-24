# Lesson 6 — Returning related records, and fixing the cycle that follows

In Lesson 5 we added the `Todo` entity and a foreign key. Todos exist in the database, but they're invisible through the API because our controllers never ask EF Core to load them. In this lesson:

1. We **scaffold a `TodosController`** so Todos have their own CRUD endpoints.
2. We **eager-load** a list's todos inline in `GET /api/TodoLists/{id}` with `.Include()`.
3. We run it, hit the infamous **object-cycle error**, understand *why* it happens, and fix it two ways.

**What we're NOT doing** (coming in later lessons):

- Opt-in eager loading via query params (`?include=todos`).
- Shaped responses / DTOs / `Select()` projections.
- Filtering (`?listId=5`), paging, validation.

Start from a copy of your Lesson 5 project (or open `lessons/Lesson6` in this repo).

---

## 1. Scaffold `TodosController`

Same command students saw in Lesson 1 — just different `-m` and controller name:

```bash
dotnet aspnet-codegenerator controller \
  -name TodosController \
  -async -api \
  -m Lesson6.Models.Todo \
  -dc Lesson6.Data.AppDbContext \
  -outDir Controllers
```

Open `Controllers/TodosController.cs`. You get the standard 5-action CRUD controller: `GetTodos`, `GetTodo(id)`, `PostTodo`, `PutTodo`, `DeleteTodo`.

### Contrast it with `TodoListsController`

Skim both files side by side. `TodosController` is **shorter** — it doesn't have a `Deletable` guard in the DELETE action, because `Todo` doesn't have a `Deletable` property. The scaffold output is strictly the model-driven baseline; any **business rule** (like "Inbox can't be deleted") is code *we* added earlier. Seeing both makes visible which parts of a controller are boilerplate and which are real business logic.

### Try it

```bash
dotnet run
curl http://localhost:5107/api/Todos
```

You should get an array of all the seeded todos — the welcome todo from `HasData`, plus the Groceries / Work tasks demo todos from `DbSeeder`. POSTing a new todo requires you to supply a valid `todoListId` — the FK is required.

---

## 2. Eager-load todos on a single list

Right now `GET /api/TodoLists/1` returns the Inbox with `"todos": null`. Let's have it return the list **with** its todos inlined. Edit `TodoListsController.GetTodoList`:

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<TodoList>> GetTodoList(int id)
{
    var todoList = await _context.TodoLists
        .Include(l => l.Todos)                    // ← load the Todos navigation too
        .FirstOrDefaultAsync(l => l.Id == id);

    if (todoList == null) return NotFound();
    return todoList;
}
```

Two notes on the change:

- **No more `FindAsync`.** `FindAsync` can't chain with `Include()` — it's a key-only lookup. The idiomatic replacement is `FirstOrDefaultAsync` with a predicate.
- **`.Include(l => l.Todos)`** translates to a **SQL LEFT JOIN**. ONE round-trip to the database loads the list and its todos together — not one query for the list and one-per-todo.

---

## 3. Run it — meet the cycle error

```bash
dotnet run
curl http://localhost:5107/api/TodoLists/1
```

You'll get a 500 response, and your console will light up with this:

```
System.Text.Json.JsonException: A possible object cycle was detected.
Path: $.Todos.TodoList.Todos.TodoList.Todos.TodoList.Todos.TodoList.Todos.TodoList.Todos...
```

Look at the `Path` — `Todos.TodoList.Todos.TodoList.Todos.TodoList...`. The serializer is recursing, and the `...` keeps going forever. Eventually it hits `System.Text.Json`'s safety limit (32 levels deep) and throws.

---

## 4. Why does this happen?

EF Core does something helpful that, in this case, is also the source of the problem: **navigation fixup**.

When we called `.Include(l => l.Todos)`, EF loaded:

- The `TodoList` row with `Id = 1`.
- All the `Todo` rows with `TodoListId = 1`.

As those Todos entered the change tracker, EF saw that the `TodoList` they reference is also in memory. It helpfully set `todo.TodoList` on each one to point back at the parent.

Now the in-memory graph looks like this:

```
TodoList { Id = 1, Todos = [
    Todo { Id = 1, TodoList = ↩ parent,  Todos = [ ... parent.Todos ... ] },
    ...
]}
```

When `System.Text.Json` walks this graph to produce JSON, it goes:

```
→ TodoList → Todos → [0] Todo → TodoList → Todos → [0] Todo → TodoList → ...
```

Infinite recursion. The serializer is right to refuse.

> **Why doesn't a GET on `/api/Todos` hit this?** Because that query doesn't `Include(t => t.TodoList)`. No parent gets loaded into the tracker alongside the todos, so the `TodoList` property stays null, and serialization is fine.

---

## 5. Fix A — `[JsonIgnore]` on the back-reference (the primary fix)

The cleanest fix is to tell the serializer: "when you encounter `Todo.TodoList`, skip it — don't even try." That's exactly what `[JsonIgnore]` does.

Edit `Models/Todo.cs`:

```csharp
using System.Text.Json.Serialization;   // ← new using

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int TodoListId { get; set; }

    [JsonIgnore]                         // ← NEW
    public TodoList? TodoList { get; set; }
}
```

Run it again:

```bash
curl http://localhost:5107/api/TodoLists/1
```

```json
{
  "id": 1,
  "title": "Inbox",
  ...,
  "todos": [
    {
      "id": 1,
      "title": "👋 Welcome to your todo app",
      "isComplete": false,
      "createdAt": "2026-01-01T00:00:00Z",
      "updatedAt": null,
      "todoListId": 1
    }
  ]
}
```

No cycle, no error. Notice what's **absent** from the todo JSON: no `todoList` property. The C# object still has it set (EF's fixup filled it in), but the serializer ignores it.

### What `[JsonIgnore]` does and doesn't do

- ✅ Skips the property during **serialization** (going out).
- ✅ Skips it during **deserialization** (coming in — clients can't inject a nested `TodoList`).
- ❌ Does **not** remove it from the C# class. `todo.TodoList.Title` still works in code.
- ❌ Does **not** affect the database — there's no column called `TodoList`, only `TodoListId`.

---

## 6. Fix B — `ReferenceHandler.IgnoreCycles` globally

There's a second approach, which solves the problem **project-wide** in one place instead of per-property.

Edit `Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
```

This tells the serializer: "when you revisit an already-seen object, emit `null` and move on." No attributes needed, no per-entity decisions — every JSON response in the app is cycle-safe.

With this setting active, **you can remove the `[JsonIgnore]`** from `Todo.TodoList`. The response then looks different:

```json
// GET /api/TodoLists/1  (with IgnoreCycles globally, no JsonIgnore)
{
  "id": 1, "title": "Inbox", "todos": [
    {
      "id": 1, "title": "...", "todoListId": 1,
      "todoList": null           // ← present, but null because it would cycle
    }
  ]
}
```

The property appears with `null` instead of being omitted. Both approaches produce valid, non-cyclic JSON; the wire format is just slightly different.

---

## 7. Which to use?

| | `[JsonIgnore]` per property | `ReferenceHandler.IgnoreCycles` globally |
|---|---|---|
| **Scope** | Exactly one property on one entity | Every JSON response in the app |
| **Reads in JSON** | Property completely absent | Property appears once as `null` |
| **Maintenance** | One attribute per cycle edge | One line, set once |
| **Predictability** | Obvious at the call site — the C# file tells you | Invisible at the call site — you have to know the global setting |
| **Escape hatch** | Remove the attribute → property shows up again | Can't exempt individual props without opting back in globally |

### Our choice for this course

We use **`[JsonIgnore]`** because:

- The rule lives **next to the property** — a reader looking at `Todo.TodoList` immediately sees why it's not in the API response.
- The global setting changes the shape of many responses at once — reviewers have to remember it exists.

Both are legitimate. Teams with lots of cycles across lots of entities often prefer the global setting; teams who value locality prefer attributes. The one thing you **shouldn't** do is both, unless you really want different behavior per property.

---

## 8. Aside — nested routes

We kept `TodosController` at `/api/Todos` — a flat route. A popular alternative for parent-child relationships is **nested routes**, like `GET /api/todolists/{listId}/todos`. The URL then reads as a path through the object graph.

You'd write it with an explicit `[Route]` instead of `[controller]`:

```csharp
[ApiController]
[Route("api/todolists/{listId:int}/todos")]
public class TodoListTodosController : ControllerBase
{
    private readonly AppDbContext _context;
    public TodoListTodosController(AppDbContext context) { _context = context; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Todo>>> GetTodosForList(int listId)
    {
        return await _context.Todos
            .Where(t => t.TodoListId == listId)
            .ToListAsync();
    }

    // GET /api/todolists/5/todos/12
    [HttpGet("{todoId:int}")]
    public async Task<ActionResult<Todo>> GetTodo(int listId, int todoId)
    {
        var todo = await _context.Todos
            .FirstOrDefaultAsync(t => t.Id == todoId && t.TodoListId == listId);
        return todo is null ? NotFound() : todo;
    }
}
```

**Pros:** reads naturally (`/api/todolists/5/todos` reads as "the todos of list 5"); every request URL asserts the parent's existence; harder to accidentally pass a todo from the wrong list.

**Cons:** more controllers for the same surface area; the flat `/api/Todos?listId=5` filter is often enough and simpler to scaffold; if a todo could belong to multiple parents (not our case, but a future "archived" entity might) nested routes become ambiguous.

We stick with **flat routes** for this course — they match what the scaffolder produces and keep controller wiring uniform. Nested routes are worth knowing about, but they're a later-stage architectural choice.

---

## Recap

- The scaffolder produces model-driven CRUD; **business rules are what you add**.
- `.Include()` turns on eager loading for a navigation property — **one SQL query**, not N+1.
- EF Core's **navigation fixup** auto-populates back-references on tracked entities, which is what creates the JSON cycle when serializing.
- Two valid fixes:
  - `[JsonIgnore]` on the back-reference (local, explicit, our choice).
  - Global `ReferenceHandler.IgnoreCycles` in `Program.cs` (sweeping, terse).
- Nested routes exist; flat routes are fine for this course.

**Next up** — making `?include=todos` an **optional** query parameter so the list endpoint can return lean OR rich responses, and then introducing **DTOs** so the wire format stops being coupled to the database schema.

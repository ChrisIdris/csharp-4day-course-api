# Lesson 7 — Query parameters: filtering and opt-in loading

Think back to the endpoint we left at the end of Lesson 6. Every `GET /api/TodoLists/{id}` now goes to the database and pulls back every todo attached to that list, whether the caller wanted them or not. That was the right move to *demonstrate* eager loading — but picture this endpoint in production. A sidebar in the UI just wants to show the names of the user's lists. A mobile widget just wants a count. A nightly export wants every list but none of their todos. Right now **all of them** pay for a JOIN and for a response body stuffed with data they never asked for.

So here's the question we're going to answer this lesson: how does a caller *ask for todos* when they want them, and skip them when they don't? The answer is **query parameters** — the part of a URL after the `?`. A caller who wants the full shape will write

    GET /api/TodoLists/5?include=todos

and a caller who wants the lean shape will just write

    GET /api/TodoLists/5

Same endpoint, two response shapes. By the end of the lesson, our API will support that — plus a second parameter, `?completed=`, that lets the caller filter which todos come back.

> **What we're NOT doing this lesson:** reshaping the response body itself (that's DTOs — Lesson 8), paging, sorting, or validation. One concept at a time — today it's query parameters.

Open your Lesson 6 project, copy it to Lesson 7, or open `lessons/Lesson7` in this repo.

---

## 1. Reading a query parameter: `?include=todos`

Right now `GetTodoList` runs `.Include(l => l.Todos)` unconditionally. Our first job is to pull that `Include()` out of the always-on path and put it behind a check: **did the caller ask for it?**

ASP.NET Core has a built-in way to read query-string values into our action's method parameters, and it's called `[FromQuery]`. You put it in front of a parameter, and ASP.NET does the wiring: if the URL contains `?include=todos`, the string `"todos"` lands in that parameter; if it doesn't, the parameter is `null`.

Add a new parameter to `GetTodoList`:

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<TodoList>> GetTodoList(int id, [FromQuery] string? include)
{
    var query = _context.TodoLists.AsQueryable();

    if (ParseInclude(include).Contains("todos"))
    {
        query = query.Include(l => l.Todos);
    }

    var todoList = await query.FirstOrDefaultAsync(l => l.Id == id);
    if (todoList == null) return NotFound();
    return todoList;
}

private static HashSet<string> ParseInclude(string? include) =>
    string.IsNullOrWhiteSpace(include)
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        : include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToHashSet(StringComparer.OrdinalIgnoreCase);
```

Let's walk through this piece by piece.

**`[FromQuery] string? include`** is the direct hand-off from URL to C#. If the request URL is `?include=todos`, your `include` variable holds the string `"todos"`; if nothing was supplied, it holds `null`. Notice we typed it as `string?` — nullable — precisely because the caller isn't obliged to supply it. Picking a nullable type isn't just about avoiding null-check warnings; it's a signal to anyone reading this code that *absent is a meaningful state*. Always use nullable types for optional query parameters, for that reason alone.

**`AsQueryable()`** is a small ritual you're going to see a lot from now on. `_context.TodoLists` is already an `IQueryable`, so the call is technically redundant. But writing `.AsQueryable()` explicitly says to the next reader "I'm about to build this query up conditionally." When you see `AsQueryable()` at the top of an action, expect one or more `if` statements each bolting a piece of query onto the variable, followed by a single `ToListAsync` or `FirstOrDefaultAsync` at the end.

**`ParseInclude`** is slightly over-built for what we need today, on purpose. Right now we only care about `?include=todos`, and you could absolutely just write `if (include == "todos")` and call it done. But query parameters like `?include=` conventionally accept comma-separated lists — one day someone will want `?include=todos,owner` — so we build the parser once, with case-insensitive matching, and every future includable is one more `.Contains("...")` check. Cheap insurance against having to redesign the signature later.

Now exercise both shapes:

```bash
curl "http://localhost:5107/api/TodoLists/1"
# → { "id": 1, ..., "todos": null }

curl "http://localhost:5107/api/TodoLists/1?include=todos"
# → { "id": 1, ..., "todos": [ ... ] }
```

Same endpoint, two response shapes, no action duplication. And notice that the first response still has a `"todos"` key — it's just `null`. That's working *with* us rather than against us. Back in Lesson 5 we deliberately declared `Todos` as `List<Todo>?` — nullable — instead of initialising it to an empty list. That design choice pays off right here: `null` means *"we didn't load them,"* while `[]` would have meant *"there are none."* Two different facts, two different representations.

---

## 2. Filtering todos: `?completed=`

Let's add a second parameter. Suppose a caller wants not just the todos, but only the *incomplete* ones — the unchecked boxes, the things still to do. The natural URL is:

    GET /api/TodoLists/5?include=todos&completed=false

Before you write any code, stop and ask: **how many distinct things does `?completed=` need to express?** Think about it for a second.

Three:

1. `?completed=true` — "give me only the completed todos."
2. `?completed=false` — "give me only the incomplete todos."
3. No `completed=` in the URL at all — "I don't care; give me everything."

Three states. How do you represent three states in C#? Not with a plain `bool`, because `bool` only has two values. When ASP.NET binds a missing query parameter to a `bool`, it has to pick one — it picks `false`. And now your API silently treats "didn't ask" as "only give me incomplete todos." That's a bug factory; six months from now someone will hit `/api/TodoLists/5?include=todos` and be confused why their completed todos are missing.

The right type for three states is a nullable `bool`: `bool?`. Three values — `true`, `false`, `null` — and ASP.NET binds "missing" to `null`. That's our signal that the caller didn't specify, in which case we don't filter at all.

Here's the updated action:

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<TodoList>> GetTodoList(
    int id,
    [FromQuery] string? include,
    [FromQuery] bool? completed)
{
    var query = _context.TodoLists.AsQueryable();

    if (ParseInclude(include).Contains("todos"))
    {
        if (completed.HasValue)
        {
            bool wantComplete = completed.Value;
            query = query.Include(l => l.Todos!.Where(t => t.IsComplete == wantComplete));
        }
        else
        {
            query = query.Include(l => l.Todos);
        }
    }

    var todoList = await query.FirstOrDefaultAsync(l => l.Id == id);
    if (todoList == null) return NotFound();
    return todoList;
}
```

Two lines in the middle deserve attention.

**`if (completed.HasValue)`** is how you ask a nullable type *"do you have a value?"*. `bool?` has three possible values — `true`, `false`, `null` — and `HasValue` is the null-check. Inside this branch we know `completed` is not null, so `completed.Value` unwraps it into a plain `bool`. We copy that into a local called `wantComplete` — "the completion state the caller asked for" — so the Where-lambda reads naturally: `t.IsComplete == wantComplete` means *"this todo's complete-state matches what we want"*. Giving EF a plain captured local also translates into a cleaner SQL parameter than unwrapping a nullable inside the expression.

**`Include(l => l.Todos!.Where(t => t.IsComplete == wantComplete))`** is a **filtered include** — a feature EF Core added in version 5. Instead of loading every todo and filtering in memory, EF translates the `.Where` right into the SQL JOIN, so the database only returns the rows we actually need. The `!` after `Todos` silences the nullable-reference-type warning, and it's safe here because this lambda is an **expression tree** that EF parses and translates to SQL — it doesn't actually dereference `Todos` at runtime.

One subtle point worth calling out. We only apply the filter *when* the caller asked for `include=todos`. If someone writes `?completed=false` **without** `?include=todos`, we ignore the filter silently — there are no todos being loaded for the filter to narrow. We could have returned a 400 Bad Request saying "your filter is meaningless without the include." That would feel technically pure. But nobody gains anything by the server being strict here, and a 400 punishes callers for a harmless over-specification. **Pick the rule that's kindest to your callers when the answer isn't ambiguous.**

Exercise both filters:

```bash
curl "http://localhost:5107/api/TodoLists/1?include=todos&completed=false"
# → list 1 with only its incomplete todos

curl "http://localhost:5107/api/TodoLists/1?include=todos&completed=true"
# → list 1 with only its complete todos
```

---

## 3. When two parameters grow to four, use a class

Our signature is already awkward:

```csharp
GetTodoList(int id, [FromQuery] string? include, [FromQuery] bool? completed)
```

Imagine how it looks when we later add paging (`page`, `pageSize`), sorting (`sort`, `order`), and a search string (`search`):

```csharp
GetTodoList(int id, [FromQuery] string? include, [FromQuery] bool? completed,
            [FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? sort, [FromQuery] string? order,
            [FromQuery] string? search)
```

That's a controller collapsing into noise. Every new filter is another parameter, another `[FromQuery]`, another thing to scroll past when you just want to find the *logic*.

ASP.NET Core has a cleaner way: bundle the query-string inputs into a plain class, tag the class parameter with `[FromQuery]`, and ASP.NET binds each of the class's public properties to the matching query-string key.

Create `Models/TodoListQuery.cs`:

```csharp
namespace Lesson7.Models;

public class TodoListQuery
{
    public string? Include { get; set; }
    public bool? Completed { get; set; }
}
```

Then the action signature becomes:

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<TodoList>> GetTodoList(int id, [FromQuery] TodoListQuery query)
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

    var todoList = await q.FirstOrDefaultAsync(l => l.Id == id);
    return todoList is null ? NotFound() : todoList;
}
```

A few things to note about how this binding works. Each public property of `TodoListQuery` binds from a matching query-string key, case-insensitive — `Include` gets whatever `?include=...` held, `Completed` gets whatever `?completed=...` held. The URL grammar doesn't change; `?include=todos&completed=false` still works exactly the same. And adding a new filter in a later lesson becomes a one-line change in `TodoListQuery` — no method-signature churn, no routing adjustments.

**Apply the same pattern to the list endpoint**, since it accepts the same knobs — just without an `id` filter:

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<TodoList>>> GetTodoLists([FromQuery] TodoListQuery query)
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

    return await q.ToListAsync();
}
```

Same query-building logic, reused.

---

## 4. `TodosController` — the same idea, simpler

The todos endpoint has one filter — `?completed=` — and no `include` concept to speak of (we'll revisit why in a moment). Here's the full action:

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<Todo>>> GetTodos([FromQuery] bool? completed)
{
    var query = _context.Todos.AsQueryable();

    if (completed is not null)
    {
        query = query.Where(t => t.IsComplete == completed.Value);
    }

    return await query.ToListAsync();
}
```

No filtered `Include` here — just a plain `.Where()` on the `IQueryable`. The `if (completed is not null)` guard means callers who skip the parameter get every todo back; callers who specify it get exactly what they asked for.

We *could* bundle this into a `TodoQuery` class for consistency with the list endpoint, but one parameter doesn't justify the ceremony. When Lesson 8 gives `TodosController` a second knob, the class pattern will earn its keep.

**So why doesn't `GET /api/Todos/{id}` support `?include=list`?** Back in Lesson 6 we put `[JsonIgnore]` on `Todo.TodoList` to break the serialization cycle. That attribute is **compile-time**; it doesn't toggle per request. We can't expose the parent in the JSON body just because the caller asked for it — the attribute always wins. To get conditionally-shaped responses, we have to stop returning entities directly and start returning **DTOs** — small classes designed for the wire. That's exactly where Lesson 8 goes.

---

## 5. Try it

Run the app (`dotnet run`) and hit it four ways. Each request pays only for what it asks for — no unconditional JOIN, no irrelevant rows:

```bash
# 1) Lean — the list alone, no todos
curl "http://localhost:5107/api/TodoLists/1"

# 2) Rich — the list with its todos included
curl "http://localhost:5107/api/TodoLists/1?include=todos"

# 3) Rich and filtered — only the incomplete todos
curl "http://localhost:5107/api/TodoLists/1?include=todos&completed=false"

# 4) Cross-list — every complete todo across every list
curl "http://localhost:5107/api/Todos?completed=true"
```

Play with the values. Try `?completed=true` without `?include=todos` (ignored silently, as discussed). Try `?include=TODOS` with uppercase (works, because our set comparer is case-insensitive). Try `?include=todos,owner` — the unknown `owner` just isn't in the set, so `.Contains("owner")` returns false and we don't load anything extra. No error, no surprise.

---

## Where this leads

We've given the caller a dial they can turn: plain GETs stay lean, richer responses come only when asked for. `[FromQuery]` on a class keeps the action signatures clean as filters accumulate. And nullable types like `bool?`, used deliberately, keep *"didn't specify"* distinct from *"specified as false."*

But we've also hit a wall. `?include=list` on a Todo doesn't work. The `[JsonIgnore]` from Lesson 6 is compile-time; it doesn't bend to a query string. To give the response shape that kind of per-request flexibility, we have to stop returning database entities as our wire format and introduce **DTOs** — small classes purpose-built for the response. That's the story of Lesson 8.

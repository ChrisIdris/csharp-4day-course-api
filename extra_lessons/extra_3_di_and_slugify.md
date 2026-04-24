# Extra Lesson 3 — Dependency injection: a `Slugifier` service, with unique slugs

Go open `http://localhost:5107/api/TodoLists/4` in a browser. You see JSON for "House chores." Now imagine the list had a human-facing URL. Which reads better: `/lists/4` or `/lists/house-chores`?

Every serious CMS, issue tracker, or social app uses the latter. The URL fragment — `house-chores` — is called a **slug**. It's lowercase, hyphenated, ASCII-only, derived from the resource's name. It's generated once, stays stable, and is **unique** within its table.

This lesson does three things:

1. Builds a **`Slugifier`** class that turns arbitrary strings into slugs.
2. Introduces it to the app via **dependency injection** — the pattern you've been using since Lesson 1 every time you took an `AppDbContext` in a controller's constructor. Now you write the other side.
3. Stores the slug in the database with a **unique index**, and handles collisions by appending `-2`, `-3`, … until we find a free one.

> **What we're NOT doing this lesson:** putting the slug logic in `DbContext.SaveChanges` (there's a good taste reason — see below), introducing an `ISlugifier` interface (a later "testability and swappability" story), or generalising the collision loop into a shared helper (also later, once we've seen the pattern twice).

Start from a copy of Extra Lesson 2, or open `extra_lessons/ExtraLesson3` in this repo.

---

## 1. What is dependency injection, really

You've been using DI since Lesson 1. Every controller constructor you've written looks like this:

```csharp
public TodoListsController(AppDbContext context)
{
    _context = context;
}
```

You *declared* a dependency (the parameter). You never *created* an `AppDbContext` yourself. ASP.NET Core's built-in DI container saw that `AppDbContext` was registered (via `builder.Services.AddDbContext<AppDbContext>(...)` in Lesson 1) and handed one to your constructor when the request came in.

That's dependency injection: **the thing being constructed announces what it needs, and something else provides it.**

The "something else" is called the **service container** — a dictionary from type to factory, populated during startup. You ask for `AppDbContext`, the container looks it up and calls the registered factory. Controllers never know *how* their dependencies are built or managed.

This lesson flips roles. So far you've been the consumer — declaring dependencies. Now you'll be the producer — registering your own service, `Slugifier`, and seeing it flow into controllers the same way `AppDbContext` does.

---

## 2. The `Slugifier` class

```csharp
// Services/Slugifier.cs
public class Slugifier
{
    private static readonly Regex NonSlugChars = new(@"[^a-z0-9-]+", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphens = new(@"-{2,}", RegexOptions.Compiled);

    public string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Strip accents: decompose into base characters + combining marks, then drop the marks.
        var normalised = input.Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(capacity: normalised.Length);
        foreach (var ch in normalised)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                stripped.Append(ch);
            }
        }

        var lower = stripped.ToString().ToLowerInvariant();
        var replaced = NonSlugChars.Replace(lower, "-");
        var collapsed = MultipleHyphens.Replace(replaced, "-");
        return collapsed.Trim('-');
    }
}
```

It's a plain class with one method. Nothing fancy, nothing "injected" into it, no interface. The algorithm:

1. **Normalize to decomposed form** (`FormD`) — turns each accented character into a base letter plus a separate combining mark (so `é` becomes `e` + a combining acute accent).
2. **Drop the combining marks** — removes accents entirely.
3. **Lowercase**.
4. **Replace any run of non-slug characters** (anything that isn't `a-z`, `0-9`, or `-`) with a single hyphen.
5. **Collapse consecutive hyphens** into one.
6. **Trim leading/trailing hyphens**.

Examples:
- `"My Weekend Plans!"` → `"my-weekend-plans"`
- `"Café — très bon"` → `"cafe-tres-bon"`
- `"  ___WEIRD___input  "` → `"weird-input"`

Notice the class has **no state that changes between calls**. Every call is pure: same input, same output, no side effects. That matters for the next step.

---

## 3. Registering the service in `Program.cs`

One line, right alongside the other service registrations:

```csharp
builder.Services.AddSingleton<Slugifier>();
```

`AddSingleton<T>()` says two things: "register `Slugifier` with the container" and "create exactly one instance, ever, and reuse it for every request."

### Service lifetimes — the three choices

| Lifetime | What the container does | When to use |
|---|---|---|
| **`AddSingleton<T>()`** | One instance for the entire app's lifetime | Stateless, thread-safe services — pure computations, cached data, shared utilities |
| **`AddScoped<T>()`** | One instance per HTTP request | Services that hold per-request state — `DbContext` is the canonical example |
| **`AddTransient<T>()`** | A fresh instance every time someone asks for one | Cheap, short-lived services, often wrappers around something else |

`Slugifier` is Singleton territory. It has no fields, no state, no connections. The same instance can serve every request without interference — in fact, a single instance *should* be reused, because allocating a new `Slugifier` (and re-compiling its regexes) on every request would be wasteful.

`AppDbContext` — by contrast — is **scoped**. It tracks entities from the current request; it holds an open DB connection. Sharing one across requests would mix data from different users. That's why `AddDbContext<T>()` defaults to scoped.

---

## 4. Adding `Slug` to `TodoList` and `Tag`

Both entities grow a new column:

```csharp
public class TodoList : IHasUpdatedAt
{
    // ...existing...
    public string Slug { get; set; } = string.Empty;
}

public class Tag : IHasUpdatedAt
{
    // ...existing...
    public string Slug { get; set; } = string.Empty;
}
```

Non-nullable `string`, defaults to `""`. We'll always populate it before saving — empty is a transitional state between "just constructed" and "saved", never a persisted state.

### Unique indexes via the Fluent API

Add these in `AppDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<TodoList>()
    .HasIndex(l => l.Slug)
    .IsUnique();

modelBuilder.Entity<Tag>()
    .HasIndex(t => t.Slug)
    .IsUnique();
```

With a real relational provider (SQL Server, Postgres, SQLite) these would generate `CREATE UNIQUE INDEX` statements. With the in-memory provider they're enforced via the change tracker — if you try to save two lists with the same slug, `SaveChanges` throws. The behaviour is consistent either way.

**Why a unique index, not a `UNIQUE` column attribute?** Because uniqueness is an index-level property, not a column-level one. And this way, if one day you want the constraint to span multiple columns (e.g., "unique slug per user"), the fluent API reads naturally: `HasIndex(l => new { l.Slug, l.OwnerId }).IsUnique()`.

---

## 5. Injecting `Slugifier` into the controller

Same pattern as `AppDbContext`, add a second constructor parameter:

```csharp
public class TodoListsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly Slugifier _slugifier;

    public TodoListsController(AppDbContext context, Slugifier slugifier)
    {
        _context = context;
        _slugifier = slugifier;
    }
    // ...
}
```

That's it. No attribute. No configuration. The DI container sees that the constructor needs a `Slugifier`, looks it up, and passes the singleton instance. Every action on the controller can now call `_slugifier.Slugify(...)`.

Same treatment for `TagsController` — add `private readonly Slugifier _slugifier`, take it in the constructor.

---

## 6. The collision-resolution loop

Here's the core algorithm, in `TodoListsController`:

```csharp
private async Task<string> ResolveUniqueSlugAsync(string fromTitle, int? excludeId)
{
    var baseSlug = _slugifier.Slugify(fromTitle);
    var candidate = baseSlug;
    int suffix = 2;
    while (await _context.TodoLists.AnyAsync(l =>
               l.Slug == candidate &&
               (excludeId == null || l.Id != excludeId)))
    {
        candidate = $"{baseSlug}-{suffix++}";
    }
    return candidate;
}
```

In prose: *compute the base slug, try it; if something else already uses it, try `<base>-2`, `<base>-3`, … until we land on a free one.*

### The `excludeId` parameter

`excludeId` is `null` on create, the row's own `Id` on update. Why?

On a POST, every other row in the table is a potential collision — we want our new row to be unique against all of them. `excludeId` is `null`, and the `(excludeId == null || l.Id != excludeId)` clause always evaluates true, so the check considers every row.

On a PUT, the row being updated already has a slug. If the title didn't change, the existing slug is still the right one — but it's *its own slug*, and without excluding self we'd see it in the `AnyAsync` result and keep appending suffixes forever. `excludeId = id` says "count every row except me."

### Using it in POST and PUT

```csharp
[HttpPost]
public async Task<ActionResult<TodoListResponse>> PostTodoList(TodoList todoList)
{
    todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: null);
    // ...add and save...
}

[HttpPut("{id}")]
public async Task<IActionResult> PutTodoList(int id, TodoList todoList)
{
    if (id != todoList.Id) return BadRequest();
    todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: id);
    // ...mark modified and save...
}
```

Whatever the client sent as the slug (if anything) is overwritten. The server is authoritative. `TagsController` gets the same treatment — a near-identical `ResolveUniqueSlugAsync`, swapping `TodoLists` for `Tags`.

**The duplication is deliberate.** Two controllers, two nearly-identical helpers. Beginners benefit from seeing the shape twice before we extract it. Later, once you've read both, the natural refactor is to put a helper on `Slugifier` that takes a `Func<string, Task<bool>>` "does this slug exist" predicate — but that's a more abstract pattern to teach on top of DI, and we're keeping this lesson narrow.

---

## 7. A deliberate non-choice: not in `SaveChanges`

You might remember from Lesson 4 that we put cross-cutting write logic (stamping `UpdatedAt`) in a `SaveChanges` override on the DbContext. Why not do the same with slug generation?

It would technically work — you could inject `Slugifier` into the DbContext, walk the change tracker for any modified/added `IHasSlug` entities, and compute slugs there. But two problems:

- **The collision loop needs to query the database.** Inside `SaveChanges`, you're already in the middle of writing — calling `AnyAsync` while save is in flight gets ugly fast.
- **`DbContext` gaining dependencies on application-layer services is a bad smell.** The DbContext's job is to talk to the database; the Slugifier's job is business logic. Keeping them on opposite sides of the arrow (controller depends on both, neither depends on the other) keeps each one testable and replaceable in isolation.

`UpdatedAt` was a good fit for `SaveChanges` because the rule was purely about what's in the tracker — no queries, no service dependencies. The slug rule isn't. Knowing which tool fits which job is part of the craft.

---

## 8. Seeding — the seeder needs the Slugifier too

`DbSeeder.Seed` now takes a `Slugifier` argument, resolved from DI in `Program.cs`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<Slugifier>());
}
```

And inside:

```csharp
var groceries = new TodoList
{
    Title = "Groceries",
    Slug = slugifier.Slugify("Groceries"),
    Description = "Weekly shop"
};
```

The seeder also reaches into the same DI container you'd use from a controller. `scope.ServiceProvider` is what gives you access to both `DbContext` (scoped) and `Slugifier` (singleton, but resolvable from any scope).

A subtlety about the Inbox seeded via `HasData`: `HasData` values must be compile-time constants, so you can't call `slugifier.Slugify(...)` inside `OnModelCreating`. The slug is hardcoded as the literal `"inbox"` — matches what `Slugifier` would produce, but a "rename the Inbox and the slug drifts" hazard to remember. In a production app you'd either keep that row out of `HasData` or accept the hand-spelled slug as documented coupling.

---

## 9. Try it

```bash
dotnet run
```

Seeded lists now have slugs:

```bash
curl http://localhost:5107/api/TodoLists | python3 -m json.tool
# "slug": "inbox"
# "slug": "groceries"
# "slug": "work-tasks"
# "slug": "house-chores"
```

Create a duplicate — watch the collision resolution:

```bash
curl -X POST http://localhost:5107/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Groceries","description":"Duplicate"}'
# → "slug": "groceries-2"

curl -X POST http://localhost:5107/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Groceries"}'
# → "slug": "groceries-3"
```

Accent and punctuation stripping:

```bash
curl -X POST http://localhost:5107/api/Tags \
     -H "Content-Type: application/json" \
     -d '{"name":"Café — très bon","color":"#abcdef"}'
# → "slug": "cafe-tres-bon"
```

The DI container, the Slugifier, the unique index, and the collision loop all interlock to give you clean URL fragments you can actually use.

---

## Wrapping up the Extra Lessons

Three extras, three layers of maturity on top of the main course. Many-to-many with a payload-carrying join table. Validation at three different levels of specificity. Dependency injection of your own service, with a real algorithmic job to do and an integrity constraint to uphold.

There are plenty of threads left dangling that any student who enjoyed these extras can keep pulling:

- **Introduce an `ISlugifier` interface** so tests can swap in a deterministic fake.
- **Generalise the collision loop** into a method on `Slugifier` that takes an "exists" predicate.
- **Put the slug in the URL** — change routes to `[Route("api/lists/{slug}")]` and look up by slug instead of id.
- **Audit the `TodoTag` with a `CreatedBy` field** once authentication lands, and start storing who attached which tag.
- **Cross-field validation via `IValidatableObject`** — enforce rules that involve more than one field on a DTO.

All stand-alone future lessons, or exercises handed to students who want to keep going.

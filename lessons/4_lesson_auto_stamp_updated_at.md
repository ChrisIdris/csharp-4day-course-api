# Lesson 4 — Auto-stamp `UpdatedAt` by overriding `SaveChanges`

Every app that tracks "when was this last changed?" eventually runs into the same question: **where should that stamping happen?**

The tempting first answer is "in the controller's PUT action, right before `SaveChanges`." It works. It's also wrong — or at least fragile. Here's why, and what to do instead.

Start from a copy of your Lesson 3 project (or open `lessons/Lesson4` in this repo).

---

## 1. Add `UpdatedAt` to the model

```csharp
public class TodoList
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // NEW — nullable because a freshly created list has never been updated.
    // We never set this by hand; the DbContext does it automatically.
    public DateTime? UpdatedAt { get; set; }

    public bool Deletable { get; set; } = true;
}
```

**Why nullable?** A row that has *never* been updated should say so. `DateTime.MinValue` would lie; `CreatedAt` would imply an update happened when it didn't. `null` is the truthful representation of "no update yet."

---

## 2. The tempting-but-wrong approach

You could set it in the PUT action:

```csharp
[HttpPut("{id}")]
public async Task<IActionResult> PutTodoList(int id, TodoList todoList)
{
    // ...
    todoList.UpdatedAt = DateTime.UtcNow;   // <- "just add this line"
    _context.Entry(todoList).State = EntityState.Modified;
    await _context.SaveChangesAsync();
    // ...
}
```

This works for PUT. But:

- If you add a PATCH endpoint later, you have to remember to stamp there too.
- If you run a background job that updates rows, same.
- If a seeder or a test fixture mutates a row, same.
- The day someone forgets — and they will — a PR lands that silently breaks the invariant. There's no warning.

A rule that has to be manually repeated in every call site is a rule that will eventually be forgotten.

---

## 3. The right approach: override `SaveChanges` on the DbContext

`DbContext.SaveChanges` (and its async sibling) is the **single funnel** every write passes through. Override it, and the stamping happens no matter who triggered the save.

Edit `Data/AppDbContext.cs`:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<TodoList> TodoLists => Set<TodoList>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // (unchanged from Lesson 3 — seeds the Inbox via HasData)
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TodoList>().HasData(
            new TodoList
            {
                Id = 1,
                Title = "Inbox",
                Description = "Your default list — cannot be deleted.",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Deletable = false
            }
        );
    }

    // NEW — auto-stamp UpdatedAt on every save.
    public override int SaveChanges()
    {
        StampUpdatedAt();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampUpdatedAt();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampUpdatedAt()
    {
        foreach (var entry in ChangeTracker.Entries<TodoList>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
```

### The walkthrough

**Why override both `SaveChanges` and `SaveChangesAsync`?** EF Core calls whichever one the caller invoked — if a controller uses `await _context.SaveChangesAsync()`, only the async one runs. A seeder that calls `db.SaveChanges()` uses the sync one. Cover both and you can't miss.

**What's `ChangeTracker.Entries<TodoList>()`?** The DbContext's **change tracker** is a live list of every entity it's loaded, plus a `State` for each one: `Unchanged`, `Modified`, `Added`, `Deleted`, `Detached`. `Entries<T>()` gives you just the tracked entries of that type.

**Why only `Modified`?**

- `Added` rows don't need `UpdatedAt` — they were just created; `CreatedAt` covers that.
- `Deleted` rows are about to disappear; stamping them is wasted work.
- `Unchanged` rows, obviously, haven't changed.

**Is this a performance problem?** No. The change tracker already knows exactly which entities are modified — we're just iterating a short list. The EF team designed `SaveChanges` as the override point specifically for cross-cutting concerns like this.

---

## 4. Run it

```bash
dotnet run
```

Create a list, then update it:

```bash
# Create one — updatedAt is null
curl -X POST http://localhost:5107/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Holiday prep","description":"Before flight"}'
# → {"id":5,"title":"Holiday prep",...,"updatedAt":null,"deletable":true}

# Update it
curl -X PUT http://localhost:5107/api/TodoLists/5 \
     -H "Content-Type: application/json" \
     -d '{"id":5,"title":"Holiday prep v2","description":"Updated","createdAt":"2026-04-24T00:00:00Z","deletable":true}'
# → 204 No Content

# Fetch it — updatedAt is now populated, CreatedAt is unchanged
curl http://localhost:5107/api/TodoLists/5
# → {"id":5,"title":"Holiday prep v2",...,"updatedAt":"2026-04-24T15:17:18...","deletable":true}

# The Inbox — never updated — still has updatedAt = null
curl http://localhost:5107/api/TodoLists/1
# → {"id":1,"title":"Inbox",...,"updatedAt":null,"deletable":false}
```

The PUT action itself didn't change at all — the stamping happened inside `SaveChangesAsync`.

---

## 5. Scaling the pattern

The approach generalises. Two directions worth knowing about:

### Opt-in via a marker interface

Right now `StampUpdatedAt` is typed to `TodoList`: `ChangeTracker.Entries<TodoList>()` only yields `TodoList` rows, so models without an `UpdatedAt` property are already safe — they never enter the loop. That breaks down the moment you have a **second** model that also wants stamping: you'd duplicate the loop, or worse, forget to.

The clean extension is a **marker interface** that models opt into:

```csharp
// Models/IHasUpdatedAt.cs
public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}
```

Models that should be stamped implement it; models that shouldn't simply don't:

```csharp
public class TodoList : IHasUpdatedAt
{
    // ...existing properties...
    public DateTime? UpdatedAt { get; set; }   // satisfies the interface
}

// Hypothetical future model: a per-item entity that doesn't track updates.
public class TodoItem
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    // no UpdatedAt — and no IHasUpdatedAt — so the stamper ignores it
}
```

Then change the loop in `StampUpdatedAt` to iterate the **interface** instead of the concrete type:

```csharp
private void StampUpdatedAt()
{
    foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
    {
        if (entry.State == EntityState.Modified)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

`ChangeTracker.Entries<T>()` filters by CLR type — and **interfaces count**. Every tracked entity whose class implements `IHasUpdatedAt` shows up; everything else is silently skipped. No reflection, no "does this property exist?" probe, no per-entity override. The type system *is* the filter.

**Why a marker interface, not the alternatives?**

- **Reflection / name probe** (`entry.Entity.GetType().GetProperty("UpdatedAt")`) — brittle: a rename silently breaks it, and you've moved a compile-time check to runtime.
- **Abstract base class `AuditableEntity`** — works, but C# is single-inheritance. The day you want a model that's both auditable *and* soft-deletable (`ISoftDeletable`), you're stuck. Interfaces compose; base classes don't.
- **Marker interface** — explicit at the class declaration, compile-checked, composable with other concerns.

### What else belongs in `SaveChanges`

Anything that must run **on every write, no matter who called it**:

- `UpdatedAt` stamping (this lesson).
- **Soft delete** — flipping an `IsDeleted` flag instead of removing rows.
- **Domain events** — collecting events from entities and publishing them after a successful save.
- **Audit logs** — writing a row to an `AuditLog` table describing what changed.

All of these become unreliable if sprinkled across controllers. All of them are airtight when centralised on the DbContext.

---

## Recap

- Put cross-cutting write logic on the **DbContext**, not in controllers.
- Override **both** `SaveChanges` and `SaveChangesAsync` — callers use whichever is handy.
- Use `ChangeTracker.Entries<T>()` and filter by `EntityState.Modified` to hit only the rows that actually changed.
- The pattern extends cleanly — interfaces for auditable entities, soft delete, domain events all ride on the same override.

From here, good next directions:
- Replace the in-memory provider with a real one (SQLite for local dev, SQL Server / Postgres for production) and introduce **migrations** — the same `OnModelCreating` and `HasData` code will work.
- Add **authorisation** so `Deletable` becomes one of several rules the API enforces per user.
- Add **validation** (data annotations like `[Required]`, `[StringLength]`) to the model.

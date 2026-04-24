# Lesson 5 — Adding a related entity: `Todo` with a foreign key

So far we have a single entity, `TodoList`. In this lesson we add a second entity, `Todo`, and a **one-to-many relationship**: each `Todo` belongs to one `TodoList`, each `TodoList` can have many `Todo`s.

This is also the lesson where the `IHasUpdatedAt` interface we teased in Lesson 4 becomes useful for real — adding the second entity is the moment where a hardcoded "stamp TodoList" override would start to rot.

**What we're NOT doing this lesson** (deliberately held back):

- Controllers for `Todo` — those come in Lesson 6.
- Returning todos inside a list response — also Lesson 6.

By the end of this lesson, the database contains todos, but they're invisible through the API. That's on purpose.

Start from a copy of your Lesson 4 project (or open `lessons/Lesson5` in this repo).

---

## 1. Define the `IHasUpdatedAt` interface

`Models/IHasUpdatedAt.cs`:

```csharp
namespace Lesson5.Models;

public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}
```

One property. That's the whole interface. Its job is to be a **marker type** we can filter on inside `SaveChanges`: any entity that implements it opts into auto-stamping. No other code in the project references it by name.

---

## 2. Make `TodoList` implement it — and add the `Todos` navigation

```csharp
namespace Lesson5.Models;

public class TodoList : IHasUpdatedAt   // ← NEW
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }   // already existed; now satisfies the interface
    public bool Deletable { get; set; } = true;

    // NEW — the inverse side of the one-to-many to Todo.
    // Nullable (NOT `= new()`): if we load a TodoList WITHOUT its todos, this stays
    // null. Serialization shows "todos": null rather than the misleading "todos": [].
    public List<Todo>? Todos { get; set; }
}
```

**Two design choices worth calling out:**

- `List<Todo>? Todos` vs `List<Todo> Todos = new()`. The second would serialize as `"todos": []` whenever we haven't loaded them — meaning "no todos" and "we didn't ask" look identical. The nullable version is **honest about missing data**.
- The collection is the **inverse** side. The authoritative side of the relationship lives on `Todo`, where the foreign-key column is declared.

---

## 3. Create the `Todo` entity

`Models/Todo.cs`:

```csharp
namespace Lesson5.Models;

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // --- Foreign key + navigation property ---

    public int TodoListId { get; set; }

    public TodoList? TodoList { get; set; }
}
```

### The FK vs the navigation

Two properties, one relationship. They're not redundant — they're complementary.

| | `TodoListId` (scalar FK) | `TodoList` (navigation) |
|---|---|---|
| **Stored in DB** | Yes — this IS the column | No — computed from the FK via a JOIN |
| **Required** | Yes, `int` is non-nullable → "every Todo must belong to a list" | No — `TodoList?` is nullable, null until loaded |
| **What you write** | Set the FK when you create a Todo: `TodoListId = groceries.Id` | Traverse in code: `todo.TodoList?.Title` |
| **EF Core default behavior** | Inferred as the FK because the name is `<NavigationName>Id` | Inferred as the related entity because of its type |

**Why both?** The FK is the minimum you need to create and persist a Todo. The navigation is for **reading** — once loaded, it lets you write `todo.TodoList.Title` instead of a second explicit query.

### "Required" vs "optional" relationships

`public int TodoListId { get; set; }` — a non-nullable `int` — tells EF this is a **required** relationship. Every Todo must belong to a list. If we'd written `public int? TodoListId { get; set; }`, it would be **optional** (Todos could float free with a null FK).

This distinction has a **real consequence** we'll mention in a moment: it changes the default delete behavior.

---

## 4. Generalise the `SaveChanges` override

In Lesson 4 the override was hardcoded to `TodoList`:

```csharp
foreach (var entry in ChangeTracker.Entries<TodoList>())
```

Now we want it to stamp `Todo.UpdatedAt` too. The naive fix is to copy-paste the loop for `Todo`. That's wrong for two reasons:

1. We'd have to remember to copy-paste again for the next auditable entity, and the one after that.
2. The *rule* is "stamp entities that have an `UpdatedAt`." Copy-paste spreads the rule; it doesn't express it.

The clean fix is to filter by the **interface**:

```csharp
private void StampUpdatedAt()
{
    // Changed from Entries<TodoList>() → Entries<IHasUpdatedAt>().
    foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
    {
        if (entry.State == EntityState.Modified)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

`ChangeTracker.Entries<T>()` filters by CLR type — and **interfaces count**. Every tracked entity whose class implements `IHasUpdatedAt` shows up; everything else is silently skipped. No reflection. No "does this property exist?" probe. The type system *is* the filter.

Adding the interface to `TodoList` and `Todo` is the entire configuration. A third auditable entity tomorrow is one line: `class SomeEntity : IHasUpdatedAt`.

---

## 5. Wire up the DbContext

`Data/AppDbContext.cs`:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<Todo> Todos => Set<Todo>();   // ← NEW

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TodoList>().HasData(
            new TodoList { /* Inbox — same as Lesson 3 */ }
        );

        // NEW — seed a welcome Todo inside the Inbox.
        modelBuilder.Entity<Todo>().HasData(
            new Todo
            {
                Id = 1,
                Title = "👋 Welcome to your todo app",
                IsComplete = false,
                TodoListId = 1,   // ← the FK VALUE (not a navigation)
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    // ...SaveChanges / SaveChangesAsync / StampUpdatedAt (now over IHasUpdatedAt)...
}
```

### HasData for related rows uses **FK values, not navigation objects**

You cannot write:

```csharp
modelBuilder.Entity<Todo>().HasData(
    new Todo { Id = 1, TodoList = inbox, ... }  // ❌ compiles, but EF will ignore TodoList
);
```

`HasData` only understands scalar values. You pass `TodoListId = 1` and EF draws the relationship from the FK.

---

## 6. Seed some demo todos (two-phase save)

`Data/DbSeeder.cs`:

```csharp
public static void Seed(AppDbContext db)
{
    if (db.TodoLists.Any(x => x.Title == "Groceries")) return;

    // Phase 1: insert parents. EF assigns their Ids when we call SaveChanges.
    var groceries = new TodoList { Title = "Groceries",  Description = "Weekly shop" };
    var work      = new TodoList { Title = "Work tasks", Description = "Sprint 42 backlog" };
    var house     = new TodoList { Title = "House chores" };

    db.TodoLists.AddRange(groceries, work, house);
    db.SaveChanges();

    // Phase 2: insert children, referencing the parents by their now-assigned Ids.
    db.Todos.AddRange(
        new Todo { Title = "Buy milk",   TodoListId = groceries.Id },
        new Todo { Title = "Buy bread",  TodoListId = groceries.Id, IsComplete = true },
        new Todo { Title = "Write PR",   TodoListId = work.Id },
        new Todo { Title = "Review PRs", TodoListId = work.Id }
    );
    db.SaveChanges();
}
```

### Why two `SaveChanges` calls?

Before the first save, `groceries.Id == 0`. EF hasn't generated the key yet. If we tried to reference `groceries.Id` in a `Todo` before saving, we'd be referencing a value that doesn't exist yet.

Calling `SaveChanges` after Phase 1 forces EF to assign the generated keys. Then we can use them in Phase 2.

> **Single-phase alternative:** you can set the navigation instead of the FK:
>
> ```csharp
> new Todo { Title = "Buy milk", TodoList = groceries }
> ```
>
> EF's change tracker figures out the relationship and resolves the FK at save time — one `SaveChanges` call. This works because the entities are in the same tracking context. We teach the two-phase approach first because the FK version is explicit about what's actually stored in the database.

---

## 7. Default delete behavior — cascade, and how to prove it

Because the relationship is **required** (non-nullable `TodoListId`), EF Core's default delete behavior is **cascade**: deleting a `TodoList` deletes all its `Todo`s in the same operation.

You haven't written `.OnDelete(...)` anywhere. This is a **convention**, not something you configured. How do you find out when you didn't write the rule?

**Three ways to verify:**

1. **The EF docs state the rule.** "Required relationships are configured to use Cascade delete by default. Optional relationships are configured to use ClientSetNull." — [EF Core cascade delete documentation](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete)

2. **Inspect the model metadata** programmatically (drop this into a throwaway endpoint or test):

    ```csharp
    var fk = db.Model.FindEntityType(typeof(Todo))!
                     .GetForeignKeys().First();
    Console.WriteLine(fk.DeleteBehavior);   // → Cascade
    ```

3. **Look at a migration.** If you were using SQL Server or SQLite with migrations, `dotnet ef migrations add InitialCreate` would produce a migration containing `ON DELETE CASCADE` on the FK constraint. We're on the in-memory provider so there's no migration, but the behavior is the same.

**To override the default** (say, "refuse to delete a list while it has todos"):

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Todo>()
        .HasOne(t => t.TodoList)
        .WithMany(l => l.Todos)
        .OnDelete(DeleteBehavior.Restrict);   // or .SetNull, .NoAction, etc.
}
```

We're keeping the default for now — it matches the intuition that a todo without a list is orphaned.

---

## 8. Run it

```bash
dotnet run
```

Hit the endpoints. Because we haven't touched the controllers — and `GetTodoList` doesn't `Include()` anything — the responses look **almost** like Lesson 4:

```json
// GET /api/TodoLists/1
{
  "id": 1,
  "title": "Inbox",
  ...,
  "todos": null        // ← new field, but null — we didn't load it
}
```

The new `"todos": null` field is visible; the data is not. This is EF Core's **explicit loading** default: navigation properties are `null` until you explicitly `Include` or `Select` them. The Todo rows are in the database (seeded via HasData + DbSeeder) — they're just invisible to the API.

Proof they're really there: hit `PUT /api/TodoLists/1` to rename it. Then `GET /api/TodoLists/1` back — the list's `updatedAt` is stamped. The override walks every `IHasUpdatedAt` in the change tracker, and it did the right thing. We'll exercise it from `Todo` too once we have a `TodosController` in Lesson 6.

---

## Recap

- **Interfaces as markers** — `IHasUpdatedAt` lets one loop in `SaveChanges` cover every auditable entity, today and tomorrow.
- **FK vs navigation** — the FK (`int TodoListId`) is what the database stores; the navigation (`TodoList?`) is for code-side traversal.
- **Required vs optional relationships** — a non-nullable `int` FK is required and defaults to **cascade delete**; optional FKs behave differently.
- **`HasData` for relations** uses scalar FK values, never navigation objects.
- **Two-phase DbSeeder** because child rows need parent Ids, which EF only assigns on `SaveChanges`.
- **Navigation properties are null until eagerly loaded** — no lazy loading by default. That's the setup for Lesson 6.

In **Lesson 6** we'll scaffold a `TodosController`, eager-load todos on a list via `.Include(l => l.Todos)`, hit the exciting `A possible object cycle was detected` error, and learn the two ways to break the cycle.

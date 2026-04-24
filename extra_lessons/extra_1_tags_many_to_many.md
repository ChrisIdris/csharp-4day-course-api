# Extra Lesson 1 — Tags: a many-to-many relation with a payload-carrying join table

Back in Lesson 5 we added a one-to-many relationship: a `TodoList` has many `Todos`, and each Todo belongs to exactly one list. That covered us all the way to Lesson 8.

But real apps also need **many-to-many**. A todo might carry multiple labels — "urgent", "home", "work". A given label appears on many todos. There's no "owner" side of this relationship; it's symmetric. Neither the `Todo` table nor the `Tag` table can store the pairing alone, because a single column only fits one value. The pairing needs its own table.

On top of that, we want one more thing: each *assignment* of a tag to a todo should be able to **override the tag's default colour**. The tag "urgent" might be red by default, but for this one particular todo you want it to render as a deeper red. That override is a property of the *pairing*, not of the tag and not of the todo — it only makes sense "on" the join.

That combination — many-to-many with per-pairing data — is why we need an **explicit join entity**. EF Core can manage a hidden join table for plain many-to-many relationships (*skip navigations*), but the hidden table only stores the two foreign keys. It has nowhere to put extra columns. As soon as the join carries data, we declare it ourselves.

> **What we're NOT doing this lesson:** validation on the new endpoints (that's Extra Lesson 2), slugs for Tag/TodoList (Extra Lesson 3), nested eager-loading (todos-on-list include their tags). One concept at a time.

Start from a copy of your Lesson 8 project, or open `extra_lessons/ExtraLesson1` in this repo.

---

## 1. The `Tag` entity

```csharp
// Models/Tag.cs
public class Tag : IHasUpdatedAt
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#808080";     // default grey
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}
```

No surprises — just another entity. It implements `IHasUpdatedAt` the same way `Todo` and `TodoList` do, so the auto-stamping from Lesson 4 covers it without a line of extra code.

Notice what the `Tag` class does **not** have: a `List<Todo> Todos` navigation. You might expect one for symmetry, but we never read the relationship directly. Every tag-to-todo link goes through `TodoTag`, which is the authoritative representation.

---

## 2. The `TodoTag` join entity

This is the interesting one.

```csharp
// Models/TodoTag.cs
public class TodoTag : IHasUpdatedAt
{
    public int TodoId { get; set; }
    public int TagId { get; set; }
    public string? ColorOverride { get; set; }     // the payload
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore] public Todo? Todo { get; set; }
    [JsonIgnore] public Tag? Tag { get; set; }
}
```

Two foreign keys. One optional payload. Two nav properties back to the parents.

**The primary key is `(TodoId, TagId)` together.** A single TodoTag row is identified by *both* ids, not by either one alone — there is no `TodoTagId`, because no one will ever look it up by "the 42nd join row." They'll always ask "is todo 5 tagged with tag 3?"

EF Core can infer a single-column primary key by naming convention (any property named `Id` or `<TypeName>Id`). A **composite** key can't be inferred — there's no naming convention that could express "these two properties together are the key." So we declare it explicitly in `AppDbContext`:

```csharp
modelBuilder.Entity<TodoTag>()
    .HasKey(tt => new { tt.TodoId, tt.TagId });
```

That single call tells EF: "the primary key of `TodoTag` is the composite of these two columns." The anonymous-object syntax (`new { ..., ... }`) is EF Core's convention for declaring composite keys; the property order is the key order.

**Why `ColorOverride` is nullable.** `null` here means "this assignment doesn't override anything; use the Tag's default colour." We never set it to `""` or `"default"` or some sentinel — an honest null expresses "nothing specified" better than any placeholder could. Same pattern you've been seeing since Lesson 5's `List<Todo>? Todos`.

---

## 3. `Todo` gets its join-side navigation

```csharp
public class Todo : IHasUpdatedAt
{
    // ...existing fields...

    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}
```

We add `TodoTags` — a collection of the join rows, not of Tags directly. When you want to know *which tags* a todo has, you walk `TodoTags` and read each `TodoTag.Tag`. Two hops instead of one, but that's the price of having the payload.

`[JsonIgnore]` for the same defence-in-depth reason we set it up on `Todo.TodoList` in Lesson 6 — our controllers return DTOs now, but if someone later writes an endpoint that returns the entity directly, we still don't want the cycle to re-emerge.

---

## 4. The DbContext — DbSets and the composite-key declaration

```csharp
public DbSet<Tag> Tags => Set<Tag>();
public DbSet<TodoTag> TodoTags => Set<TodoTag>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<TodoTag>()
        .HasKey(tt => new { tt.TodoId, tt.TagId });

    // ...existing HasData for TodoList + Todo...
}
```

Two new `DbSet`s, one model-builder call. That's all the EF-side wiring.

---

## 5. A scaffolded `TagsController` (same command as always)

Tags have full CRUD — create, read, update, delete — just like todos and lists. Run the scaffolder exactly as in Lesson 1, just with `-m .Tag`:

```bash
dotnet aspnet-codegenerator controller \
  -name TagsController \
  -async -api \
  -m ExtraLesson1.Models.Tag \
  -dc ExtraLesson1.Data.AppDbContext \
  -outDir Controllers
```

You'll get the familiar five actions. Then rewrite them to use a `TagResponse` DTO and a `CreateTagRequest` DTO, following the pattern from Lesson 8. (The DTO files are in `Dtos/` if you want to peek ahead.)

---

## 6. Attach and detach — new endpoints on `TodosController`

The *interesting* new endpoints aren't on `TagsController` — they're on `TodosController`. Attaching a tag to a todo is an operation *about the todo*, so its URL lives under `/api/Todos/{id}`:

```csharp
[HttpPost("{id}/tags")]
public async Task<ActionResult<TodoTagResponse>> AttachTag(int id, AttachTagRequest request)
{
    var todoExists = await _context.Todos.AnyAsync(t => t.Id == id);
    if (!todoExists) return NotFound();

    var tag = await _context.Tags.FindAsync(request.TagId);
    if (tag is null)
    {
        return Problem(
            detail: $"Tag with id {request.TagId} does not exist.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var already = await _context.TodoTags.FindAsync(id, request.TagId);
    if (already is not null)
    {
        return Problem(
            detail: "This tag is already attached to this todo.",
            statusCode: StatusCodes.Status409Conflict);
    }

    var tt = new TodoTag
    {
        TodoId = id,
        TagId = request.TagId,
        ColorOverride = request.ColorOverride
    };
    _context.TodoTags.Add(tt);
    await _context.SaveChangesAsync();

    tt.Tag = tag;
    return TodoTagResponse.FromEntity(tt);
}

[HttpDelete("{id}/tags/{tagId}")]
public async Task<IActionResult> DetachTag(int id, int tagId)
{
    var tt = await _context.TodoTags.FindAsync(id, tagId);
    if (tt is null) return NotFound();

    _context.TodoTags.Remove(tt);
    await _context.SaveChangesAsync();
    return NoContent();
}
```

A few things worth pointing out.

**The sub-routes.** `POST /api/Todos/{id}/tags` and `DELETE /api/Todos/{id}/tags/{tagId}` read as English: "post a tag onto this todo" / "delete this tag from this todo." The URL reflects the containment relationship — a TodoTag row is meaningful only *inside* its parent Todo.

**The 409 Conflict for "already attached."** The composite PK would catch this at the database level — EF would throw on `SaveChanges` if you tried to insert a duplicate `(TodoId, TagId)` row. But the exception is ugly, and the client-facing message would be worse. A friendly 409 tells the caller exactly what happened.

**`FindAsync(id, request.TagId)` for a composite-keyed entity.** `FindAsync` accepts the key values in the same order they were declared in `HasKey(tt => new { tt.TodoId, tt.TagId })`. Get the order wrong and you'll silently look up a different row — the arguments are typed as `object[]`, so the compiler doesn't help. Double-check.

**The Tag nav fix-up after save.** `TodoTagResponse.FromEntity(tt)` needs `tt.Tag` to be populated so it can read the tag's Name and default Color. We already have `tag` in scope (we loaded it for the existence check), so we assign it to `tt.Tag` before returning. Otherwise the mapper throws.

---

## 7. `TodoTagResponse` — where "effective colour" is computed

```csharp
public record TodoTagResponse(
    int TagId,
    string Name,
    string EffectiveColor
)
{
    public static TodoTagResponse FromEntity(TodoTag tt)
    {
        if (tt.Tag is null)
        {
            throw new InvalidOperationException(
                "TodoTag.Tag must be loaded before calling FromEntity — add .ThenInclude(tt => tt.Tag).");
        }

        return new TodoTagResponse(
            tt.TagId,
            tt.Tag.Name,
            tt.ColorOverride ?? tt.Tag.Color
        );
    }
}
```

The line that does the interesting work is:

```csharp
tt.ColorOverride ?? tt.Tag.Color
```

The `??` null-coalescing operator — "use the left side if it's not null, otherwise fall back to the right side." That's the entire `EffectiveColor` rule. The client never has to know that `ColorOverride` exists. They get *the colour to render*, already resolved.

This is a great example of a **DTO doing computation on the way out**. The database stores the override (nullable) and the default (on the Tag) as two separate pieces of information. The wire shape merges them into one value that's always populated. From the client's perspective, every TodoTag has a colour, full stop.

---

## 8. `TodoResponse` now embeds tags

```csharp
public record TodoResponse(
    int Id,
    string Title,
    bool IsComplete,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TodoListId,
    List<TodoTagResponse>? Tags   // <-- new
)
{
    public static TodoResponse FromEntity(Todo todo) =>
        new(
            todo.Id, todo.Title, todo.IsComplete, todo.CreatedAt, todo.UpdatedAt, todo.TodoListId,
            todo.TodoTags?.Select(TodoTagResponse.FromEntity).ToList()
        );
}
```

Same honest-null pattern: if we don't `.Include` the `TodoTags` navigation, the mapper sees `null` and the response carries `"tags": null`. If we did include it, each join row gets mapped through `TodoTagResponse.FromEntity`.

---

## 9. The `Include` chain on reads

The mapping function demands that `tt.Tag` be loaded. So every query that returns Todos needs to tell EF to pull the chain: `Todo → TodoTags → Tag`. That's `Include` followed by `ThenInclude`.

In `TodosController.GetTodos`:

```csharp
var query = _context.Todos
    .Include(t => t.TodoTags!)
        .ThenInclude(tt => tt.Tag)
    .AsQueryable();
```

**`Include(t => t.TodoTags!)`** loads every join row for each Todo. Good, but we still can't render a `TodoTagResponse` from that alone — we need the Tag's Name and default Color.

**`.ThenInclude(tt => tt.Tag)`** tells EF: *"and for each of those join rows, also load the related Tag."* The `!` on `TodoTags!` silences the nullable warning; same safe expression-tree story as the filtered `Include` from Lesson 7.

Behind the scenes this becomes one SQL query with two joins — Todo LEFT JOIN TodoTag LEFT JOIN Tag. Not N+1. EF is doing the right thing.

---

## 10. Seeding tags (three phases now)

`DbSeeder` grows a third phase: tags, then assignments. Tags first (we need their Ids), then the `TodoTag` rows that reference them:

```csharp
db.Todos.AddRange(buyMilk, buyBread, writePR, reviewPRs);
db.SaveChanges();

var urgent  = new Tag { Name = "urgent", Color = "#ff0000" };
var homeTag = new Tag { Name = "home",   Color = "#00aa00" };
var workTag = new Tag { Name = "work",   Color = "#0066cc" };
db.Tags.AddRange(urgent, homeTag, workTag);
db.SaveChanges();

db.TodoTags.AddRange(
    new TodoTag { TodoId = buyMilk.Id, TagId = homeTag.Id },
    new TodoTag { TodoId = writePR.Id, TagId = workTag.Id },
    // ColorOverride demo: same tag, but this assignment renders as a deeper red.
    new TodoTag { TodoId = writePR.Id, TagId = urgent.Id, ColorOverride = "#cc0000" },
    new TodoTag { TodoId = reviewPRs.Id, TagId = workTag.Id }
);
db.SaveChanges();
```

The `writePR` todo now has *two* tags, and one of them overrides the default red with a deeper shade. That's the smoke-test case you'll want to hit first after `dotnet run`.

---

## 11. Try it

```bash
dotnet run
```

```bash
# Seeded tags
curl "http://localhost:5107/api/Tags" | python3 -m json.tool

# Write PR — has 2 tags, one with an override
curl "http://localhost:5107/api/Todos/4" | python3 -m json.tool
# → "tags": [{"tagId":1,"name":"urgent","effectiveColor":"#cc0000"},
#            {"tagId":3,"name":"work","effectiveColor":"#0066cc"}]

# Create a new tag and attach it to the Welcome todo with an override
curl -X POST "http://localhost:5107/api/Tags" \
     -H "Content-Type: application/json" \
     -d '{"name":"errand","color":"#ff9900"}'

curl -X POST "http://localhost:5107/api/Todos/1/tags" \
     -H "Content-Type: application/json" \
     -d '{"tagId":4,"colorOverride":"#cc7700"}'

curl "http://localhost:5107/api/Todos/1" | python3 -m json.tool
# → "tags": [{"tagId":4,"name":"errand","effectiveColor":"#cc7700"}]
```

Detaching:

```bash
curl -X DELETE "http://localhost:5107/api/Todos/2/tags/2"
# → 204 No Content

curl "http://localhost:5107/api/Todos/2" | python3 -m json.tool
# → "tags": []   (loaded but now empty, vs. null = not loaded)
```

Notice the difference between `"tags": []` and `"tags": null`. After a detach, the todo's tags collection exists but has no rows — empty array. When the endpoint doesn't `Include` tags at all, the response has `null`. Two different facts, two different shapes.

---

## Where this leads

We've got a many-to-many relationship with a payload, end to end: data model, join entity, controller endpoints, DTOs with computed fields, seeded demo data. The tag vocabulary grows independently of the todos; each assignment can tweak its presentation without changing the tag globally.

Two threads lead out of here:

**Extra Lesson 2** — our new endpoints accept anything. `POST /api/Tags` with `{"name":"","color":"not a color"}` happily creates a garbage tag. Time to validate.

**Extra Lesson 3** — `/api/Tags/4` is a fine URL for the machine, but `/tags/errand` is friendlier for humans. We'll add slugs, enforce their uniqueness, and use dependency injection to bring in a `Slugifier` service.

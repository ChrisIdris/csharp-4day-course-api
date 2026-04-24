namespace Postgres3.Models;

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    // NEW — added in this lesson to demonstrate a schema change.
    // Non-nullable int so every row must have a value; the migration EF generates
    // backfills existing rows with `defaultValue: 0` (the int default). Making
    // this `int?` instead would have produced a nullable column and existing rows
    // would default to NULL — a different but equally valid choice.
    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // --- Foreign key + navigation property ---------------------------------------

    // Scalar FK stored as the TodoListId column. Required (non-nullable int):
    // every Todo MUST belong to a TodoList. Attempting to insert without it fails.
    public int TodoListId { get; set; }

    // Navigation property — the parent TodoList object. Loaded only when we explicitly
    // Include() it; otherwise null. We deliberately leave [JsonIgnore] off here — in
    // Lesson 6 we'll Include() Todos on a list, see the serializer explode on the
    // reference cycle (TodoList → Todos → Todo → TodoList → ...), and THEN add the
    // fix. Teaching the pain first makes the fix stick.
    public TodoList? TodoList { get; set; }
}

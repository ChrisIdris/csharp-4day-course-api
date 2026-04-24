namespace Lesson6.Models;

public class TodoList : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // From IHasUpdatedAt — same story as before; AppDbContext stamps it automatically.
    public DateTime? UpdatedAt { get; set; }

    // Soft "is-deletable" flag. DeleteTodoList checks this before removing a row.
    // Default true — only special lists (e.g. the Inbox seeded via HasData) are non-deletable.
    public bool Deletable { get; set; } = true;

    // Inverse side of the one-to-many to Todo. Nullable (not `= new()`): when a TodoList
    // is loaded WITHOUT its todos, serialization shows "todos": null rather than a
    // misleading "todos": []. In Lesson 6 we'll start Including this navigation.
    public List<Todo>? Todos { get; set; }
}

namespace Postgres1.Models;

public class TodoList : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool Deletable { get; set; } = true;

    // Inverse side of the one-to-many to Todo. Nullable (not `= new()`): when a TodoList
    // is loaded WITHOUT its todos, serialization shows "todos": null rather than a
    // misleading "todos": []. In Lesson 6 we'll start Including this navigation.
    public List<Todo>? Todos { get; set; }
}

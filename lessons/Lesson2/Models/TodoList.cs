namespace Lesson2.Models;

public class TodoList
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Soft "is-deletable" flag. DeleteTodoList checks this before removing a row.
    // Default true — only special lists (e.g. a user's Inbox, seeded in Lesson 3) are non-deletable.
    public bool Deletable { get; set; } = true;
}

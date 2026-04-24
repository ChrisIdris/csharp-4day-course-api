namespace Lesson4.Models;

public class TodoList
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Nullable — a freshly created list has *never* been updated, so null is the truthful value.
    // We never set this by hand in controllers; AppDbContext's SaveChanges override stamps it
    // automatically whenever a tracked TodoList is in the Modified state.
    public DateTime? UpdatedAt { get; set; }

    // Soft "is-deletable" flag. DeleteTodoList checks this before removing a row.
    // Default true — only special lists (e.g. a user's Inbox, seeded below via HasData) are non-deletable.
    public bool Deletable { get; set; } = true;
}

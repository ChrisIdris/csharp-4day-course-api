namespace ExtraLesson3.Models;

public class TodoList : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    // New in Extra Lesson 3 — URL-safe, unique-per-table version of Title.
    // Populated by the controller using the injected Slugifier, with a collision-resolution
    // loop that appends "-2", "-3", ... if the base slug is taken. The unique index on
    // this column (configured in AppDbContext.OnModelCreating) guarantees there's only
    // one row per slug across the table.
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool Deletable { get; set; } = true;

    public List<Todo>? Todos { get; set; }
}

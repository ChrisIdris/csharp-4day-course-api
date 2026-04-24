using System.Text.Json.Serialization;

namespace ExtraLesson1.Models;

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int TodoListId { get; set; }

    [JsonIgnore]
    public TodoList? TodoList { get; set; }

    // New in Extra Lesson 1 — the Todo side of the many-to-many relation with Tag,
    // expressed via the explicit TodoTag join. We never wire a List<Tag> directly;
    // the join entity IS the relation.
    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}

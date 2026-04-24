using System.Text.Json.Serialization;

namespace AuthLesson3.Models;

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

    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}

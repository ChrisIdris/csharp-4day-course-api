using System.Text.Json.Serialization;

namespace Lesson8.Models;

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int TodoListId { get; set; }

    // [JsonIgnore] is now belt-and-braces. Every controller in this lesson returns
    // response DTOs (see Dtos/TodoResponse.cs) which don't include this back-reference
    // at all, so the serializer never actually sees it. The attribute stays as a
    // safety net: if a future endpoint forgets the DTO and returns an entity directly,
    // the cycle from Lesson 6 won't resurface.
    [JsonIgnore]
    public TodoList? TodoList { get; set; }
}

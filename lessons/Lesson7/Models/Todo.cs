using System.Text.Json.Serialization;

namespace Lesson7.Models;

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int TodoListId { get; set; }

    // [JsonIgnore] — when TodoListsController.GetTodoList uses .Include(l => l.Todos),
    // EF Core's navigation fixup points each loaded Todo back at its parent TodoList.
    // That creates a graph cycle (TodoList → Todos → Todo → TodoList → ...), and
    // System.Text.Json throws "A possible object cycle was detected."
    //
    // [JsonIgnore] tells the serializer: "this property exists in C#, but skip it during
    // JSON (de)serialization." Code-side traversal still works; the wire format just
    // doesn't include the back-reference.
    [JsonIgnore]
    public TodoList? TodoList { get; set; }
}

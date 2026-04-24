using System.Text.Json.Serialization;

namespace Lesson6.Models;

public class Todo : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Satisfies IHasUpdatedAt — AppDbContext.SaveChanges stamps this automatically.
    public DateTime? UpdatedAt { get; set; }

    // --- Foreign key + navigation property ---------------------------------------

    // Scalar FK stored as the TodoListId column. Required (non-nullable int):
    // every Todo MUST belong to a TodoList. Attempting to insert without it fails.
    public int TodoListId { get; set; }

    // Navigation property — the parent TodoList. EF loads it when you Include() it,
    // and ALSO sets it automatically (via "navigation fixup") whenever it loads a Todo
    // alongside its parent — e.g. when TodoListsController.GetTodoList uses
    //   _context.TodoLists.Include(l => l.Todos)
    // EF loads the todos AND points each one back at the parent list. Without the
    // [JsonIgnore] below, that back-reference creates a graph cycle and System.Text.Json
    // throws a JsonException when it tries to serialize it.
    //
    // [JsonIgnore] says: "this property exists in C#, but skip it during JSON
    // (de)serialization." Code-side traversal still works; the wire format just
    // doesn't include it.
    [JsonIgnore]
    public TodoList? TodoList { get; set; }
}

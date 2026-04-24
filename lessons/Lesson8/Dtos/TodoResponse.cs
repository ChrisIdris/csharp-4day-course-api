using Lesson8.Models;

namespace Lesson8.Dtos;

// Response DTO for a single Todo. The shape is defined by what belongs on the wire,
// not what happens to exist on the entity:
//   - No `TodoList` navigation back-reference — DTOs only contain what we put on them,
//     so the Lesson 6 cycle cannot form in the first place.
//   - `TodoListId` stays — callers still need to know which list this todo belongs to.
public record TodoResponse(
    int Id,
    string Title,
    bool IsComplete,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TodoListId
)
{
    // Static factory — turns an entity into its wire shape. Naming it `FromEntity`
    // by convention means call sites read naturally: `TodoResponse.FromEntity(someTodo)`.
    // This is the single place the entity-to-DTO mapping lives; if we ever add another
    // field to Todo, only this method and the record's parameter list need updating.
    public static TodoResponse FromEntity(Todo todo) =>
        new(todo.Id, todo.Title, todo.IsComplete, todo.CreatedAt, todo.UpdatedAt, todo.TodoListId);
}

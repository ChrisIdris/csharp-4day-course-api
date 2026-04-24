namespace Lesson8.Dtos;

// Request DTO for POST /api/Todos/with-new-list.
//
// Describes the OPERATION — a single call that creates both a new list AND a new
// todo inside it — rather than any one entity's shape. This is a shape you couldn't
// express by accepting a Todo or a TodoList alone; it only exists on the request
// side, which is exactly where request DTOs earn their keep.
public record CreateTodoWithNewListRequest(
    string TodoTitle,
    bool IsComplete,
    string ListTitle,
    string? ListDescription
);

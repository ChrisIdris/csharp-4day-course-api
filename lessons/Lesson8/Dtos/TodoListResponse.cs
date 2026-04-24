using Lesson8.Models;

namespace Lesson8.Dtos;

// Response DTO for a TodoList.
//
// Notice what's MISSING compared to the TodoList entity: the `Deletable` flag.
// That field exists in the database to drive our DELETE controller's business rule,
// but it isn't part of the public API contract. Clients learn that a list is
// non-deletable by trying to DELETE it and receiving a 400 Problem response.
//
// (Reasonable people disagree here — some APIs expose `Deletable` so the UI can hide
// the delete button up-front. This lesson picks the "hidden" side to illustrate the
// mechanics of hiding; pick whichever fits your API's needs.)
public record TodoListResponse(
    int Id,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<TodoResponse>? Todos
)
{
    public static TodoListResponse FromEntity(TodoList list) =>
        new(
            list.Id,
            list.Title,
            list.Description,
            list.CreatedAt,
            list.UpdatedAt,
            // If list.Todos is null (the navigation wasn't eager-loaded), we want the
            // response's Todos to stay null too — same "honest null" principle from
            // Lesson 5. If it WAS loaded, project every entity through the child DTO.
            list.Todos?.Select(TodoResponse.FromEntity).ToList()
        );
}

using ExtraLesson3.Models;

namespace ExtraLesson3.Dtos;

public record TodoListResponse(
    int Id,
    string Title,
    // New in Extra Lesson 3 — the generated slug travels with the response so clients
    // can build canonical URLs like /lists/my-weekend or /tags/urgent.
    string Slug,
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
            list.Slug,
            list.Description,
            list.CreatedAt,
            list.UpdatedAt,
            list.Todos?.Select(TodoResponse.FromEntity).ToList()
        );
}

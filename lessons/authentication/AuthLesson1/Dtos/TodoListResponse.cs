using AuthLesson1.Models;

namespace AuthLesson1.Dtos;

public record TodoListResponse(
    int Id,
    string Title,
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

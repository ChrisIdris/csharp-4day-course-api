using ExtraLesson2.Models;

namespace ExtraLesson2.Dtos;

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
            list.Todos?.Select(TodoResponse.FromEntity).ToList()
        );
}

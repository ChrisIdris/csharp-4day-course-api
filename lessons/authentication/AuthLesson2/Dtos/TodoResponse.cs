using AuthLesson2.Models;

namespace AuthLesson2.Dtos;

public record TodoResponse(
    int Id,
    string Title,
    bool IsComplete,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TodoListId,
    List<TodoTagResponse>? Tags
)
{
    public static TodoResponse FromEntity(Todo todo) =>
        new(
            todo.Id,
            todo.Title,
            todo.IsComplete,
            todo.CreatedAt,
            todo.UpdatedAt,
            todo.TodoListId,
            todo.TodoTags?.Select(TodoTagResponse.FromEntity).ToList()
        );
}

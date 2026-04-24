using ExtraLesson1.Models;

namespace ExtraLesson1.Dtos;

public record TodoResponse(
    int Id,
    string Title,
    bool IsComplete,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TodoListId,
    // New in Extra Lesson 1. Nullable — stays null when the TodoTags navigation
    // wasn't eager-loaded on the query.
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

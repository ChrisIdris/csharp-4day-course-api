using System.ComponentModel.DataAnnotations;

namespace AuthLesson1.Dtos;

public record CreateTodoWithNewListRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string TodoTitle,

    bool IsComplete,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string ListTitle,

    [StringLength(500)]
    string? ListDescription
);

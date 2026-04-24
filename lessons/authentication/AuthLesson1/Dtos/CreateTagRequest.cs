using System.ComponentModel.DataAnnotations;
using AuthLesson1.Validation;

namespace AuthLesson1.Dtos;

public record CreateTagRequest(
    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Name must be 1-50 characters.")]
    string Name,

    [Required]
    [HexColor]
    string Color
);

using System.ComponentModel.DataAnnotations;
using ExtraLesson2.Validation;

namespace ExtraLesson2.Dtos;

// Validation on request DTOs happens via data annotations. [ApiController] sees any
// model-binding or attribute failure and short-circuits with a 400 Problem response
// BEFORE the action even runs — we get validation for free with zero controller code.
public record CreateTagRequest(
    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Name must be 1-50 characters.")]
    string Name,

    [Required]
    [HexColor]
    string Color
);

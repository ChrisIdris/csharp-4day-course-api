using System.ComponentModel.DataAnnotations;
using ExtraLesson3.Validation;

namespace ExtraLesson3.Dtos;

public record AttachTagRequest(
    [Range(1, int.MaxValue, ErrorMessage = "TagId must be a positive integer.")]
    int TagId,

    [HexColor]
    string? ColorOverride
);

using System.ComponentModel.DataAnnotations;
using AuthLesson2.Validation;

namespace AuthLesson2.Dtos;

public record AttachTagRequest(
    [Range(1, int.MaxValue, ErrorMessage = "TagId must be a positive integer.")]
    int TagId,

    [HexColor]
    string? ColorOverride
);

using System.ComponentModel.DataAnnotations;
using ExtraLesson2.Validation;

namespace ExtraLesson2.Dtos;

public record AttachTagRequest(
    [Range(1, int.MaxValue, ErrorMessage = "TagId must be a positive integer.")]
    int TagId,

    // ColorOverride is optional (nullable). [HexColor] alone passes for null, so the
    // field only has to match the pattern when the caller actually supplies a value.
    [HexColor]
    string? ColorOverride
);

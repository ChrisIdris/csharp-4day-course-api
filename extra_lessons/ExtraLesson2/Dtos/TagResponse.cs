using ExtraLesson2.Models;

namespace ExtraLesson2.Dtos;

public record TagResponse(
    int Id,
    string Name,
    string Color,
    DateTime CreatedAt,
    DateTime? UpdatedAt
)
{
    public static TagResponse FromEntity(Tag tag) =>
        new(tag.Id, tag.Name, tag.Color, tag.CreatedAt, tag.UpdatedAt);
}

using AuthLesson2.Models;

namespace AuthLesson2.Dtos;

public record TagResponse(
    int Id,
    string Name,
    string Slug,
    string Color,
    DateTime CreatedAt,
    DateTime? UpdatedAt
)
{
    public static TagResponse FromEntity(Tag tag) =>
        new(tag.Id, tag.Name, tag.Slug, tag.Color, tag.CreatedAt, tag.UpdatedAt);
}

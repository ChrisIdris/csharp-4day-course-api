using ExtraLesson2.Models;

namespace ExtraLesson2.Dtos;

public record TodoTagResponse(
    int TagId,
    string Name,
    string EffectiveColor
)
{
    public static TodoTagResponse FromEntity(TodoTag tt)
    {
        if (tt.Tag is null)
        {
            throw new InvalidOperationException(
                "TodoTag.Tag must be loaded before calling FromEntity — add .ThenInclude(tt => tt.Tag) to the query.");
        }

        return new TodoTagResponse(
            tt.TagId,
            tt.Tag.Name,
            tt.ColorOverride ?? tt.Tag.Color
        );
    }
}

using ExtraLesson1.Models;

namespace ExtraLesson1.Dtos;

// The shape we embed inside a TodoResponse to describe one tag on that todo.
//
// `EffectiveColor` is the interesting field — it's not stored anywhere. It's computed
// by the FromEntity mapper as "the override if set, otherwise the tag's default."
// The client never has to know that ColorOverride even exists; it just sees the
// final colour it should render.
public record TodoTagResponse(
    int TagId,
    string Name,
    string EffectiveColor
)
{
    public static TodoTagResponse FromEntity(TodoTag tt)
    {
        // We require the Tag navigation to be loaded — the mapper has no way to look
        // it up itself. A thrown exception here points any caller at their missing
        // .ThenInclude(tt => tt.Tag).
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

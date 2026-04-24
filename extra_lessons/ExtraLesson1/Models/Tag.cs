using System.Text.Json.Serialization;

namespace ExtraLesson1.Models;

public class Tag : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Default colour used by any TodoTag that doesn't override it. Stored as a hex string
    // like "#ff0000"; we don't try to parse/validate the format yet — that's Extra Lesson 2.
    public string Color { get; set; } = "#808080";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // The Tag ↔ Todo relationship goes through the TodoTag join entity — never directly.
    // We [JsonIgnore] because tag-centric responses (e.g. GET /api/Tags) don't need to
    // enumerate every todo that uses this tag.
    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}

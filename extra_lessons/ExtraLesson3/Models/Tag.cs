using System.Text.Json.Serialization;

namespace ExtraLesson3.Models;

public class Tag : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Same story as TodoList.Slug — URL-safe unique identifier derived from Name by
    // the Slugifier service. Unique index configured in AppDbContext.
    public string Slug { get; set; } = string.Empty;

    public string Color { get; set; } = "#808080";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}

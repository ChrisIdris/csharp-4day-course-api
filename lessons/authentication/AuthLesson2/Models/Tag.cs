using System.Text.Json.Serialization;

namespace AuthLesson2.Models;

public class Tag : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Color { get; set; } = "#808080";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}

using System.Text.Json.Serialization;

namespace AuthLesson3.Models;

public class Tag : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Color { get; set; } = "#808080";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Same per-user ownership pattern as TodoList — Alice's "urgent" tag and
    // Bob's "urgent" tag are two separate rows with separate Ids.
    public string OwnerId { get; set; } = string.Empty;

    [JsonIgnore]
    public ApplicationUser? Owner { get; set; }

    [JsonIgnore]
    public List<TodoTag>? TodoTags { get; set; }
}

using System.Text.Json.Serialization;

namespace ExtraLesson3.Models;

public class TodoTag : IHasUpdatedAt
{
    public int TodoId { get; set; }
    public int TagId { get; set; }
    public string? ColorOverride { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore] public Todo? Todo { get; set; }
    [JsonIgnore] public Tag? Tag { get; set; }
}

using System.Text.Json.Serialization;

namespace ExtraLesson1.Models;

// Explicit join entity for the many-to-many between Todo and Tag. "Explicit" means we
// declare the join as its own class rather than letting EF Core manage a hidden skip-
// navigation table — necessary here because the join CARRIES DATA: ColorOverride.
//
// The primary key is COMPOSITE: (TodoId, TagId). That pair uniquely identifies one
// tag-on-todo assignment. EF Core can't infer composite PKs by convention, so we
// configure it in AppDbContext.OnModelCreating.
public class TodoTag : IHasUpdatedAt
{
    public int TodoId { get; set; }
    public int TagId { get; set; }

    // Per-assignment override. null means "use the Tag's default colour." Lets a team use
    // the same Tag (say "urgent") across many todos but render one of them a different shade.
    public string? ColorOverride { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore] public Todo? Todo { get; set; }
    [JsonIgnore] public Tag? Tag { get; set; }
}

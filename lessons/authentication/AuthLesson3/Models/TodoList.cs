using System.Text.Json.Serialization;

namespace AuthLesson3.Models;

public class TodoList : IHasUpdatedAt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool Deletable { get; set; } = true;

    // New in Auth Lesson 3 — who this list belongs to. FK to AspNetUsers.Id.
    // Non-nullable: every list MUST have an owner. Populated by the controller
    // from the authenticated user's claims; any client-supplied OwnerId is
    // overwritten before save. This is the load-bearing column that makes
    // per-user scoping possible.
    public string OwnerId { get; set; } = string.Empty;

    [JsonIgnore]
    public ApplicationUser? Owner { get; set; }

    public List<Todo>? Todos { get; set; }
}

namespace Lesson5.Models;

// Opt-in marker for "this entity should have its UpdatedAt stamped on every save."
// The stamping logic lives on AppDbContext.SaveChanges/SaveChangesAsync and applies
// to every IHasUpdatedAt the change tracker knows about. Adding auditing to a new
// entity is one line — implement the interface — with no change to the DbContext.
public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}

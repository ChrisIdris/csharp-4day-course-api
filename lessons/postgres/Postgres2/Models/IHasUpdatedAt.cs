namespace Postgres2.Models;

// Marker interface: entities that implement it opt into auto-stamping by
// AppDbContext.SaveChanges. The loop there filters ChangeTracker.Entries<IHasUpdatedAt>(),
// so a new entity joins the stamping regime just by implementing this one property.
public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}

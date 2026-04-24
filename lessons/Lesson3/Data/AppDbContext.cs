using Lesson3.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson3.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    // OnModelCreating is EF Core's hook for describing your schema in code: relationships, keys,
    // indexes, and — what we use here — *seed data* via HasData.
    //
    // HasData is for baseline rows that are part of the schema itself — rows every instance of
    // the app must have. A per-user "Inbox" list that cannot be deleted is the perfect fit: it's
    // reference data, it has a known Id, and it never needs a runtime-computed value.
    //
    // For dynamic or environment-specific seeding (demo data, timestamps, values that depend on
    // services), we use the imperative DbSeeder class instead — see Data/DbSeeder.cs.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // HasData requires explicit primary-key values (EF won't auto-generate them here because
        // the row is declared at model-build time, before any database connection exists).
        // Fix CreatedAt to a constant — HasData values must be compile-time constants for the same
        // reason; you can't call DateTime.UtcNow here.
        modelBuilder.Entity<TodoList>().HasData(
            new TodoList
            {
                Id = 1,
                Title = "Inbox",
                Description = "Your default list — cannot be deleted.",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Deletable = false
            }
        );
    }
}

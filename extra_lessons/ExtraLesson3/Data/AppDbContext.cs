using ExtraLesson3.Models;
using Microsoft.EntityFrameworkCore;

namespace ExtraLesson3.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TodoTag> TodoTags => Set<TodoTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TodoTag>()
            .HasKey(tt => new { tt.TodoId, tt.TagId });

        // New in Extra Lesson 3 — unique indexes on the slug columns. With a real database
        // provider this becomes a CREATE UNIQUE INDEX statement and the DB rejects any
        // duplicate insert at the storage layer. InMemory enforces the same rule via the
        // change tracker, so the behaviour is consistent between dev and prod.
        modelBuilder.Entity<TodoList>()
            .HasIndex(l => l.Slug)
            .IsUnique();

        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<TodoList>().HasData(
            new TodoList
            {
                Id = 1,
                Title = "Inbox",
                // HasData values must be compile-time constants, so we can't call Slugifier here.
                // We hand-spell the slug for the seeded Inbox row — it matches what Slugifier
                // would produce from "Inbox". If you ever rename the seeded Inbox, remember
                // to update this too (or delete it and let a dev-time seeder populate it).
                Slug = "inbox",
                Description = "Your default list — cannot be deleted.",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Deletable = false
            }
        );

        modelBuilder.Entity<Todo>().HasData(
            new Todo
            {
                Id = 1,
                Title = "👋 Welcome to your todo app",
                IsComplete = false,
                TodoListId = 1,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    public override int SaveChanges()
    {
        StampUpdatedAt();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampUpdatedAt();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampUpdatedAt()
    {
        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}

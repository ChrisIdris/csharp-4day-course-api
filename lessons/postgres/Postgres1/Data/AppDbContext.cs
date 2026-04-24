using Postgres1.Models;
using Microsoft.EntityFrameworkCore;

namespace Postgres1.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    // The other side of the one-to-many. Every EF entity type we want to query needs a DbSet.
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        // Seed a welcome Todo inside the Inbox. Two things to notice:
        //   1) we pass TodoListId = 1 (the FK VALUE) — not a TodoList navigation object.
        //      HasData works only with scalars; it knows nothing about navigation properties.
        //   2) Id = 1 is the Todo's own primary key. Nothing stops two different tables from
        //      both having an Id = 1; they're independent sequences.
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
        // Generalised in Lesson 5: was Entries<TodoList>(), now Entries<IHasUpdatedAt>().
        // Every entity implementing the interface — TodoList, Todo, anything added later —
        // gets stamped by this one loop. ChangeTracker.Entries<T>() filters by CLR type,
        // and INTERFACES COUNT as a filter. No reflection; no per-entity override.
        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}

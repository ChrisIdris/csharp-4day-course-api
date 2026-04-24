using Lesson6.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson6.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    // NEW in Lesson 5 — the other side of the one-to-many. Every EF entity type we want to
    // query needs a DbSet here (or to be declared via DbContext.Set<T>() on the fly).
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

        // NEW — seed a welcome Todo inside the Inbox. Two things to notice:
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

    // --------------------------------------------------------------------------------------------
    // Auto-stamp TodoList.UpdatedAt on every save.
    //
    // Overriding SaveChanges (and SaveChangesAsync — EF calls one or the other depending on the
    // caller) is the canonical EF Core place to put "run this on every write" logic. It runs for
    // *any* code path that mutates the DB: controllers, seeders, background jobs, tests. That's a
    // big step up from setting UpdatedAt in the PUT action by hand — if you ever forgot, the
    // timestamp would silently go stale.
    //
    // We walk ChangeTracker.Entries<TodoList>() (EF's live list of entities it's tracking) and
    // stamp only those in the Modified state. Added rows don't get an UpdatedAt — they were just
    // created. Deleted rows don't either — they're about to disappear.
    // --------------------------------------------------------------------------------------------
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
        // Changed in Lesson 5: was Entries<TodoList>(), now Entries<IHasUpdatedAt>().
        // Every entity that implements the interface — TodoList, Todo, anything we add
        // later — gets stamped by this one loop. ChangeTracker.Entries<T>() filters by
        // CLR type, and INTERFACES COUNT as a filter. No reflection; no per-entity override.
        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}

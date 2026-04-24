using ExtraLesson1.Models;
using Microsoft.EntityFrameworkCore;

namespace ExtraLesson1.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<Todo> Todos => Set<Todo>();

    // New in Extra Lesson 1 — the Tag table and its join with Todo.
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TodoTag> TodoTags => Set<TodoTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // EF Core discovers a single-column primary key by convention (any property
        // named `Id` or `<TypeName>Id`). A COMPOSITE primary key has to be declared
        // explicitly — there's no naming convention that could express it.
        modelBuilder.Entity<TodoTag>()
            .HasKey(tt => new { tt.TodoId, tt.TagId });

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

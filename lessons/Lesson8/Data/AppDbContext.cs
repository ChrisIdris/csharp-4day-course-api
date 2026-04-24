using Lesson8.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson8.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
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

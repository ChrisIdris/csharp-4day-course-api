using AuthLesson1.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthLesson1.Data;

// New base class in Auth Lesson 1: IdentityDbContext<ApplicationUser>.
// This tells EF Core to create the Identity tables (AspNetUsers, AspNetRoles,
// AspNetUserClaims, AspNetUserRoles, AspNetUserLogins, AspNetUserTokens,
// AspNetRoleClaims) as part of the same model. Our existing DbSets keep working
// unchanged — we just gain seven new tables and the UserManager API.
public class AppDbContext : IdentityDbContext<ApplicationUser>
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
        // CRITICAL: call base FIRST. IdentityDbContext configures all its tables
        // (keys, indexes, relationships) inside this override. Skip it and you
        // get a runtime error about the missing AspNetUsers table on first query.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TodoTag>()
            .HasKey(tt => new { tt.TodoId, tt.TagId });

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

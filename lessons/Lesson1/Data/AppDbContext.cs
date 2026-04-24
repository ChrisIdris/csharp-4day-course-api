// `using` brings types into scope so we can write `DbContext` instead of the full
// `Microsoft.EntityFrameworkCore.DbContext` everywhere.
using Lesson1.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson1.Data;

// DbContext is EF Core's "unit of work + session": it tracks entities in memory, generates SQL,
// and manages the connection to the database. Our subclass tells EF Core which entities we care
// about (via DbSet<T> properties) and, optionally, how they're configured.
public class AppDbContext : DbContext
{
    // Constructor parameter: DI will hand us a `DbContextOptions<AppDbContext>` that was configured
    // in Program.cs (provider, connection string, logging, etc.).
    // `: base(options)` forwards it up to the DbContext base class, which stores and uses it.
    //
    // Taking options as a parameter (instead of hard-coding them inside this class) is what makes
    // the context testable and swappable between providers — in-memory for lessons/tests, SQLite
    // for local dev, SQL Server/Postgres in production, all without touching this file.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // A DbSet<T> represents a table and is our query entry point:
    //   _context.TodoLists.Where(t => ...).ToListAsync()
    //
    // Expression-bodied form (`=> Set<TodoList>()`) is preferred over `{ get; set; }` because:
    //   - `Set<T>()` always returns the correct DbSet instance managed by this context
    //   - it avoids a "non-nullable property is uninitialized" compiler warning you'd otherwise
    //     need to suppress with `= null!;`
    public DbSet<TodoList> TodoLists => Set<TodoList>();
}

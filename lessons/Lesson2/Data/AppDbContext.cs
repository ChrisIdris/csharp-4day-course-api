using Lesson2.Models;
using Microsoft.EntityFrameworkCore;

namespace Lesson2.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
}

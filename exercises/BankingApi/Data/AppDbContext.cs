using BankingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();

}

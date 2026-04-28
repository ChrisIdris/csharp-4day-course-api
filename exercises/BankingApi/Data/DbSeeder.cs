using BankingApi.Models;

namespace BankingApi.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (!context.Branches.Any())
        {
            context.Branches.AddRange(
                new Branch { Name = "Main Branch", Address = "123 Main St" },
                new Branch { Name = "Second Branch", Address = "456 Second St" }
            );
            context.SaveChanges();
        }
        if (!context.Accounts.Any())
        {
            context.Accounts.AddRange(
                new Account { AccountNumber = "ACC-1000" },
                new Account { AccountNumber = "ACC-1001" },
                new Account { AccountNumber = "ACC-1002" }
            );
            context.SaveChanges();
        }
    }
}
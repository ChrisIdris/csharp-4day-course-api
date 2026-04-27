namespace BankingApi.Models;

public class Account
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


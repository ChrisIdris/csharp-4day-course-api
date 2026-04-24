using AuthLesson3.Models;
using AuthLesson3.Services;
using Microsoft.AspNetCore.Identity;

namespace AuthLesson3.Data;

public static class DbSeeder
{
    // Now async, and takes UserManager so it can create Identity users with
    // hashed passwords. Called from Program.cs which resolves everything from DI.
    public static async Task SeedAsync(
        AppDbContext db,
        Slugifier slugifier,
        UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync("alice@example.com") is not null)
        {
            return;
        }

        var alice = await CreateUserAsync(userManager, "alice@example.com", "Passw0rd!");
        var bob   = await CreateUserAsync(userManager, "bob@example.com",   "Passw0rd!");

        await SeedUserDataAsync(db, slugifier, alice.Id);
        await SeedUserDataAsync(db, slugifier, bob.Id);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager, string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"Failed to seed user {email}: {errors}");
        }
        return user;
    }

    // Every seeded user gets a fresh copy of the demo data. Each user has their
    // own Inbox (Deletable = false), their own Groceries / Work lists, their
    // own tag vocabulary. Slugs are per-user so "inbox" and "groceries" collide
    // only across the same owner's rows — not across the whole table.
    private static async Task SeedUserDataAsync(AppDbContext db, Slugifier slugifier, string ownerId)
    {
        var inbox = new TodoList
        {
            OwnerId = ownerId,
            Title = "Inbox",
            Slug = slugifier.Slugify("Inbox"),
            Description = "Your default list — cannot be deleted.",
            Deletable = false
        };
        var groceries = new TodoList
        {
            OwnerId = ownerId,
            Title = "Groceries",
            Slug = slugifier.Slugify("Groceries"),
            Description = "Weekly shop"
        };
        var workList = new TodoList
        {
            OwnerId = ownerId,
            Title = "Work tasks",
            Slug = slugifier.Slugify("Work tasks"),
            Description = "Sprint 42 backlog"
        };

        db.TodoLists.AddRange(inbox, groceries, workList);
        await db.SaveChangesAsync();

        var welcome  = new Todo { Title = "👋 Welcome to your todo app", TodoListId = inbox.Id };
        var buyMilk  = new Todo { Title = "Buy milk", TodoListId = groceries.Id };
        var writePR  = new Todo { Title = "Write PR", TodoListId = workList.Id };
        db.Todos.AddRange(welcome, buyMilk, writePR);
        await db.SaveChangesAsync();

        var urgent = new Tag { OwnerId = ownerId, Name = "urgent", Slug = slugifier.Slugify("urgent"), Color = "#ff0000" };
        var home   = new Tag { OwnerId = ownerId, Name = "home",   Slug = slugifier.Slugify("home"),   Color = "#00aa00" };
        db.Tags.AddRange(urgent, home);
        await db.SaveChangesAsync();

        db.TodoTags.Add(new TodoTag { TodoId = writePR.Id, TagId = urgent.Id });
        await db.SaveChangesAsync();
    }
}

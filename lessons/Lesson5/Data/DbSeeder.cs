using Lesson5.Models;

namespace Lesson5.Data;

public static class DbSeeder
{
    // Called once at app startup (from Program.cs). Imperative seeding path — dev/demo
    // content, anything that can't be compile-time constants. The Inbox (and its Welcome
    // todo) is seeded via HasData; this method only adds the rest.
    public static void Seed(AppDbContext db)
    {
        // Idempotency sentinel: if "Groceries" is already in the DB, we've already seeded.
        if (db.TodoLists.Any(x => x.Title == "Groceries"))
        {
            return;
        }

        // ---- Phase 1: save parent TodoLists so EF assigns their Ids ------------------
        //
        // Todos have a REQUIRED TodoListId — they can't be inserted before the parent
        // row exists. We could set nav properties instead (Todo { TodoList = groceries })
        // and let EF figure out the FKs in a single SaveChanges, but the explicit
        // two-phase approach is clearer about what's happening.
        var groceries = new TodoList { Title = "Groceries",  Description = "Weekly shop" };
        var work      = new TodoList { Title = "Work tasks", Description = "Sprint 42 backlog" };
        var house     = new TodoList { Title = "House chores" };

        db.TodoLists.AddRange(groceries, work, house);
        db.SaveChanges();

        // ---- Phase 2: save Todos, referencing the parents by their now-assigned Ids --
        db.Todos.AddRange(
            new Todo { Title = "Buy milk",   TodoListId = groceries.Id },
            new Todo { Title = "Buy bread",  TodoListId = groceries.Id, IsComplete = true },
            new Todo { Title = "Write PR",   TodoListId = work.Id },
            new Todo { Title = "Review PRs", TodoListId = work.Id }
        );
        db.SaveChanges();
    }
}

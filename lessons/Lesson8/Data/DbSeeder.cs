using Lesson8.Models;

namespace Lesson8.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.TodoLists.Any(x => x.Title == "Groceries"))
        {
            return;
        }

        var groceries = new TodoList { Title = "Groceries",  Description = "Weekly shop" };
        var work      = new TodoList { Title = "Work tasks", Description = "Sprint 42 backlog" };
        var house     = new TodoList { Title = "House chores" };

        db.TodoLists.AddRange(groceries, work, house);
        db.SaveChanges();

        db.Todos.AddRange(
            new Todo { Title = "Buy milk",   TodoListId = groceries.Id },
            new Todo { Title = "Buy bread",  TodoListId = groceries.Id, IsComplete = true },
            new Todo { Title = "Write PR",   TodoListId = work.Id },
            new Todo { Title = "Review PRs", TodoListId = work.Id }
        );
        db.SaveChanges();
    }
}

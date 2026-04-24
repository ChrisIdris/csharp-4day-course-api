using ExtraLesson2.Models;

namespace ExtraLesson2.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.TodoLists.Any(x => x.Title == "Groceries"))
        {
            return;
        }

        var groceries = new TodoList { Title = "Groceries",  Description = "Weekly shop" };
        var workList  = new TodoList { Title = "Work tasks", Description = "Sprint 42 backlog" };
        var house     = new TodoList { Title = "House chores" };

        db.TodoLists.AddRange(groceries, workList, house);
        db.SaveChanges();

        var buyMilk   = new Todo { Title = "Buy milk",   TodoListId = groceries.Id };
        var buyBread  = new Todo { Title = "Buy bread",  TodoListId = groceries.Id, IsComplete = true };
        var writePR   = new Todo { Title = "Write PR",   TodoListId = workList.Id };
        var reviewPRs = new Todo { Title = "Review PRs", TodoListId = workList.Id };

        db.Todos.AddRange(buyMilk, buyBread, writePR, reviewPRs);
        db.SaveChanges();

        var urgent  = new Tag { Name = "urgent", Color = "#ff0000" };
        var homeTag = new Tag { Name = "home",   Color = "#00aa00" };
        var workTag = new Tag { Name = "work",   Color = "#0066cc" };
        db.Tags.AddRange(urgent, homeTag, workTag);
        db.SaveChanges();

        db.TodoTags.AddRange(
            new TodoTag { TodoId = buyMilk.Id,   TagId = homeTag.Id },
            new TodoTag { TodoId = writePR.Id,   TagId = workTag.Id },
            new TodoTag { TodoId = writePR.Id,   TagId = urgent.Id,  ColorOverride = "#cc0000" },
            new TodoTag { TodoId = reviewPRs.Id, TagId = workTag.Id }
        );
        db.SaveChanges();
    }
}

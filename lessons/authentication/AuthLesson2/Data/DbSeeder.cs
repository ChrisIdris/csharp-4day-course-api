using AuthLesson2.Models;
using AuthLesson2.Services;

namespace AuthLesson2.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db, Slugifier slugifier)
    {
        if (db.TodoLists.Any(x => x.Title == "Groceries"))
        {
            return;
        }

        var groceries = new TodoList { Title = "Groceries",  Slug = slugifier.Slugify("Groceries"),  Description = "Weekly shop" };
        var workList  = new TodoList { Title = "Work tasks", Slug = slugifier.Slugify("Work tasks"), Description = "Sprint 42 backlog" };
        var house     = new TodoList { Title = "House chores", Slug = slugifier.Slugify("House chores") };

        db.TodoLists.AddRange(groceries, workList, house);
        db.SaveChanges();

        var buyMilk   = new Todo { Title = "Buy milk",   TodoListId = groceries.Id };
        var buyBread  = new Todo { Title = "Buy bread",  TodoListId = groceries.Id, IsComplete = true };
        var writePR   = new Todo { Title = "Write PR",   TodoListId = workList.Id };
        var reviewPRs = new Todo { Title = "Review PRs", TodoListId = workList.Id };

        db.Todos.AddRange(buyMilk, buyBread, writePR, reviewPRs);
        db.SaveChanges();

        var urgent  = new Tag { Name = "urgent", Slug = slugifier.Slugify("urgent"), Color = "#ff0000" };
        var homeTag = new Tag { Name = "home",   Slug = slugifier.Slugify("home"),   Color = "#00aa00" };
        var workTag = new Tag { Name = "work",   Slug = slugifier.Slugify("work"),   Color = "#0066cc" };
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

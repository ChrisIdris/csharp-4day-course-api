using ExtraLesson1.Models;

namespace ExtraLesson1.Data;

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

        // Capture the Todo references so we can tag them in the next phase.
        var buyMilk   = new Todo { Title = "Buy milk",   TodoListId = groceries.Id };
        var buyBread  = new Todo { Title = "Buy bread",  TodoListId = groceries.Id, IsComplete = true };
        var writePR   = new Todo { Title = "Write PR",   TodoListId = workList.Id };
        var reviewPRs = new Todo { Title = "Review PRs", TodoListId = workList.Id };

        db.Todos.AddRange(buyMilk, buyBread, writePR, reviewPRs);
        db.SaveChanges();

        // New in Extra Lesson 1 — seed a small tag vocabulary and attach a handful of
        // tags to our demo todos. Save the Tags first so their Ids are available.
        var urgent  = new Tag { Name = "urgent", Color = "#ff0000" };
        var homeTag = new Tag { Name = "home",   Color = "#00aa00" };
        var workTag = new Tag { Name = "work",   Color = "#0066cc" };
        db.Tags.AddRange(urgent, homeTag, workTag);
        db.SaveChanges();

        db.TodoTags.AddRange(
            new TodoTag { TodoId = buyMilk.Id,   TagId = homeTag.Id },
            new TodoTag { TodoId = writePR.Id,   TagId = workTag.Id },
            // ColorOverride demo: same "urgent" tag, but this one renders as a deeper red.
            new TodoTag { TodoId = writePR.Id,   TagId = urgent.Id,  ColorOverride = "#cc0000" },
            new TodoTag { TodoId = reviewPRs.Id, TagId = workTag.Id }
        );
        db.SaveChanges();
    }
}

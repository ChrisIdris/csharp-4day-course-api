using Lesson3.Models;

namespace Lesson3.Data;

public static class DbSeeder
{
    // Called once at app startup (from Program.cs). This is the *imperative* seeding path,
    // for data that can't live in HasData — typically dev/demo content, and anything that needs
    // a runtime-computed value (DateTime.UtcNow, a hashed password, a value from IConfiguration).
    //
    // The Inbox row is NOT added here — it's part of the model (see AppDbContext.OnModelCreating)
    // and is already present by the time this method runs.
    public static void Seed(AppDbContext db)
    {
        // Idempotency: if our demo rows are already in the DB, bail out. We can't just check
        // "any rows exist" because HasData has already inserted the Inbox. Picking a title that's
        // unique to our demo data is a simple sentinel. (For the in-memory provider the DB is
        // fresh every run anyway, but the habit matters the moment you switch to a real DB.)
        if (db.TodoLists.Any(x => x.Title == "Groceries"))
        {
            return;
        }

        db.TodoLists.AddRange(
            new TodoList { Title = "Groceries",   Description = "Weekly shop" },
            new TodoList { Title = "Work tasks",  Description = "Sprint 42 backlog" },
            new TodoList { Title = "House chores" }
        );

        db.SaveChanges();
    }
}

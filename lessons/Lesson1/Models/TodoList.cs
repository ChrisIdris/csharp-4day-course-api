// File-scoped namespace (C# 10+): applies to everything in this file.
// Equivalent to the older `namespace Lesson1.Models { ... }` block form, just less indentation.
namespace Lesson1.Models;

// A plain C# class acting as our "entity" — one instance represents one row in the TodoLists table.
// EF Core will discover it because we expose a DbSet<TodoList> on the DbContext (see AppDbContext.cs).
public class TodoList
{
    // By convention, EF Core treats a property named `Id` (or `TodoListId`) as the primary key.
    // `{ get; set; }` is an auto-property: the compiler generates a hidden backing field for you.
    public int Id { get; set; }

    // `string` is a non-nullable reference type (nullable reference types are on by default in new
    // .NET templates). Assigning `= string.Empty` gives it a safe default so the compiler doesn't
    // warn that Title might be null when a TodoList is constructed without setting it.
    public string Title { get; set; } = string.Empty;

    // The `?` on `string?` marks the property as *nullable*: null is an allowed value.
    // Use this for optional fields where "not provided" is meaningful (e.g. an unfilled description).
    public string? Description { get; set; }

    // Default to UTC (not DateTime.Now) so timestamps are timezone-agnostic — servers and clients
    // in different timezones will always agree on what this value means.
    // Every new TodoList instance gets "now at construction" unless explicitly overridden.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // A soft "is-deletable" flag. Controllers (see DeleteTodoList) must check this before removing
    // a row. Default `true` means "unless we say otherwise, a list can be deleted" — only special
    // lists like a user's Inbox get Deletable = false (seeded in Lesson 3 via HasData).
    public bool Deletable { get; set; } = true;
}

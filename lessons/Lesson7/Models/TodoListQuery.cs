namespace Lesson7.Models;

// Bundles the query-string inputs for list endpoints. ASP.NET binds each public property
// from the matching query-string key (case-insensitive), e.g. ?include=todos&completed=false.
// Using a class instead of multiple [FromQuery] parameters keeps the action signature tidy
// as more filters are added in later lessons — no signature churn per new field.
public class TodoListQuery
{
    // Comma-separated list of related collections the caller wants eager-loaded.
    // Today only "todos" is recognised; the grammar leaves room for "?include=todos,owner".
    public string? Include { get; set; }

    // Three-state filter: true (only complete), false (only incomplete), null (both).
    // Nullable is load-bearing: a plain `bool` would collapse "unspecified" and "false"
    // into one, silently meaning "only incomplete todos".
    public bool? Completed { get; set; }
}

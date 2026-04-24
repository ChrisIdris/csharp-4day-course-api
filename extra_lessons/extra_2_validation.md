# Extra Lesson 2 — Validation, three ways

Our API happily accepts whatever the client sends. `POST /api/Tags` with `{"name":"","color":"orange"}` creates a tag with an empty name and a colour that isn't even a colour. `POST /api/Todos/5/tags` with `{"tagId":-1}` has no business succeeding. We've been living off the assumption that clients will behave, and that's not an assumption any real API can afford.

This lesson adds validation. But "validation" is a catch-all — different kinds of rules want different tools. Three kinds, three techniques, one lesson:

1. **Shape rules** — "Name must be non-empty", "Color must be a hex string". Expressed as **data annotations** on the request DTO. The framework auto-rejects bad input before your action even runs.
2. **Business rules involving runtime data** — "A todo may have at most 5 tags." You can't know this until you count the rows in the database. Expressed as **manual checks inside the controller**.
3. **Reusable shape rules not in the standard toolbox** — "Color must be a hex string, applied wherever we take a hex string." Expressed as a **custom validation attribute**.

> **What we're NOT doing this lesson:** FluentValidation (a great alternative library once the built-in system gets crowded), cross-field validation via `IValidatableObject`, or validation on route parameters like `/api/Tags/{id:int:min(1)}`. One lesson, three techniques.

Start from a copy of Extra Lesson 1, or open `extra_lessons/ExtraLesson2` in this repo.

---

## 1. Technique 1 — data annotations on request DTOs

Open `Dtos/CreateTagRequest.cs`. The Extra Lesson 1 version was:

```csharp
public record CreateTagRequest(string Name, string Color);
```

Two strings. No rules. No enforcement. We'll add rules now:

```csharp
using System.ComponentModel.DataAnnotations;

public record CreateTagRequest(
    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Name must be 1-50 characters.")]
    string Name,

    [Required]
    [HexColor]                          // we'll build this attribute in Technique 3
    string Color
);
```

Three attributes, each one a **rule** ASP.NET Core checks automatically before your action runs:

- **`[Required]`** says the field must be present in the JSON body and non-null.
- **`[StringLength(50, MinimumLength = 1)]`** says the string must be between 1 and 50 characters.
- **`[HexColor]`** (custom — coming in Technique 3) says the string must match `#RGB` or `#RRGGBB`.

These are called **data annotations** — they live in `System.ComponentModel.DataAnnotations`. .NET ships a couple dozen of them: `[Required]`, `[StringLength]`, `[Range]`, `[RegularExpression]`, `[MinLength]`, `[MaxLength]`, `[EmailAddress]`, `[Url]`, and more.

### How the framework uses them

Every controller in this project is tagged with `[ApiController]` up at the class level (we've had it since Lesson 1). That attribute turns on one piece of behaviour we haven't drawn attention to until now: **automatic model-state validation**. Before your action runs, ASP.NET Core:

1. Binds the JSON body to the `CreateTagRequest` record.
2. Runs every data annotation on every field.
3. If any annotation fails, **returns a 400 Bad Request automatically** with a response body that lists every failure, grouped by field.
4. Your action doesn't even get called. You write zero code for this.

Try POSTing a bad tag:

```bash
curl -X POST http://localhost:5107/api/Tags \
     -H "Content-Type: application/json" \
     -d '{"name":"","color":"#ff0000"}'
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": [
      "The Name field is required.",
      "Name must be 1-50 characters."
    ]
  }
}
```

Notice both `[Required]` *and* `[StringLength]` fired at the same time. Annotations are independent — each one runs, and all the failures are reported together. That means callers can fix everything wrong with their request in a single retry, not one iteration per rule.

### Annotations compose

Same treatment for `AttachTagRequest`:

```csharp
public record AttachTagRequest(
    [Range(1, int.MaxValue, ErrorMessage = "TagId must be a positive integer.")]
    int TagId,

    [HexColor]
    string? ColorOverride
);
```

`[Range]` — minimum and maximum inclusive, no 0 or negative Ids. And notice `ColorOverride` is *still nullable*; `[HexColor]` will pass for null values because optional fields should stay optional. The attribute only enforces the pattern when something is actually supplied.

And on `CreateTodoWithNewListRequest`:

```csharp
public record CreateTodoWithNewListRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string TodoTitle,

    bool IsComplete,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string ListTitle,

    [StringLength(500)]
    string? ListDescription
);
```

Every request DTO in the project now has rules.

---

## 2. Technique 2 — manual validation in the controller

Data annotations can express a lot, but they can't see the database. Consider the rule: **a todo can have at most 5 tags**. No attribute can enforce that — the check requires counting the existing `TodoTag` rows for this particular todo, which means touching EF Core, which means running inside an action.

This is a **business rule involving runtime data**. It lives in the controller, not on the DTO.

In `TodosController`:

```csharp
private const int MaxTagsPerTodo = 5;

[HttpPost("{id}/tags")]
public async Task<ActionResult<TodoTagResponse>> AttachTag(int id, AttachTagRequest request)
{
    // Attribute-level validation on AttachTagRequest has already passed by the time we
    // enter this method — [ApiController] would have 400'd if Range or HexColor failed.
    // From here on, we're enforcing *business* rules, not *shape* rules.

    var todoExists = await _context.Todos.AnyAsync(t => t.Id == id);
    if (!todoExists) return NotFound();

    var existingCount = await _context.TodoTags.CountAsync(tt => tt.TodoId == id);
    if (existingCount >= MaxTagsPerTodo)
    {
        return Problem(
            detail: $"A todo can have at most {MaxTagsPerTodo} tags; this one already has {existingCount}.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    // ...rest of the attach logic...
}
```

The pattern: **count, compare, return `Problem()` if over the limit.**

Two details worth noticing.

**The order of the checks matters.** We verify the todo exists first, then count, then check the tag exists, then check the (todo, tag) pair isn't already attached. Each failure returns the most specific status we can — 404 for a missing todo, 400 for our max-tag rule, 400 for a missing tag, 409 for a conflict. That gives clients precise feedback instead of a single "something was wrong" 400.

**`Problem()` is the RFC-7807 "problem details" helper** we've used since Lesson 1. Using it consistently — rather than a mix of `BadRequest(string)`, `Problem()`, and custom JSON — means every failure from the API has the same shape. Clients write one error-handler that understands `{ title, status, detail }` and they're covered.

### The constant matters

Pulling `MaxTagsPerTodo = 5` out as a `const` has two benefits beyond avoiding magic numbers:

- It's visible at the top of the class, so when someone asks "what's the tag limit?" they find it without reading the action body.
- It gets injected into the error message automatically (`{MaxTagsPerTodo}`), so the constant and the message can't drift apart.

Try it after the seed data:

```bash
# Write PR (id=4) already has 2 tags. Attach 3 more to reach 5.
curl -X POST http://localhost:5107/api/Todos/4/tags \
     -H "Content-Type: application/json" -d '{"tagId":4}'
curl -X POST http://localhost:5107/api/Todos/4/tags \
     -H "Content-Type: application/json" -d '{"tagId":2}'
# (also attach a 5th — any tag that isn't already on it)

# Now try to attach the 6th:
curl -X POST http://localhost:5107/api/Todos/4/tags \
     -H "Content-Type: application/json" -d '{"tagId":99}'
```

```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "A todo can have at most 5 tags; this one already has 5."
}
```

---

## 3. Technique 3 — a custom validation attribute

Look at `CreateTagRequest.Color` and `AttachTagRequest.ColorOverride`. They're both hex-colour fields. Both need the same rule: `#RGB` or `#RRGGBB`, nothing else. And `[RegularExpression]` exists, yes — but writing the same regex literal in two different DTOs (and any future DTO that carries a colour) invites drift. One place becomes `^#...$`, another becomes `^#[0-9a-f]{3,6}$`, they disagree in subtle ways, bugs happen.

The better tool is a **custom validation attribute** — our own reusable rule, applied by name.

```csharp
// Validation/HexColorAttribute.cs
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HexColorAttribute : ValidationAttribute
{
    private static readonly Regex Pattern = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is null) return true;    // optional fields handled by [Required] if needed
        return value is string s && Pattern.IsMatch(s);
    }

    public override string FormatErrorMessage(string name) =>
        $"The {name} field must be a hex colour like #ff0000 or #fc0.";
}
```

### What's happening in this class

**Inheriting from `ValidationAttribute`.** That's the base class every built-in annotation inherits from too. `[Required]`, `[StringLength]`, `[Range]` — all `ValidationAttribute` subclasses. By inheriting from it, our attribute plugs into the same machinery `[ApiController]` already invokes, with no additional wiring. Declare the attribute, use it on a property, it just works.

**Overriding `IsValid(object?)`.** This is the simplest of three overloads. Return `true` for valid, `false` for invalid. We return `true` for null (let `[Required]` worry about nullness — compose attributes, don't duplicate their responsibilities). We return `true` only when the value is a string that matches the regex.

**`FormatErrorMessage`.** Provides the default message when no `ErrorMessage = "..."` is given at the usage site. The `{name}` argument is the property name (e.g., "Color") so messages self-identify.

**`[AttributeUsage(...)]` at the top.** Tells the compiler where this attribute can be applied. `Property` for regular classes, `Parameter` for positional record parameters. Without this the compiler might allow or reject the attribute in surprising places.

**`sealed`.** Common for utility attributes — if you don't plan for subclassing, sealing documents that and lets the JIT optimise calls.

**Compiled regex.** `RegexOptions.Compiled` pays a small cost once and then matches fast on every subsequent call. Appropriate because validation runs on every request.

### Using it

```csharp
public record CreateTagRequest(
    [Required] [StringLength(50, MinimumLength = 1)] string Name,
    [Required] [HexColor] string Color
);
```

That's the payoff. One line at the use site. The rule is named, self-documenting, reusable.

Try a bad colour:

```bash
curl -X POST http://localhost:5107/api/Tags \
     -H "Content-Type: application/json" \
     -d '{"name":"bad","color":"orange"}'
```

```json
{
  "errors": {
    "Color": ["The Color field must be a hex colour like #ff0000 or #fc0."]
  }
}
```

---

## 4. Which technique for which problem

Three tools, three jobs.

| If the rule is… | …use |
|---|---|
| A **shape** check on an input field (required, length, range, pattern, matches a built-in annotation) | **Data annotations** on the DTO |
| A **business rule** that depends on the current database state, other entities, time, or anything else the DTO can't see | **Manual check** in the controller, returning `Problem()` |
| A **shape check** that's specific to your domain and reused across multiple DTOs | **Custom `ValidationAttribute`** |

These compose. A DTO can carry data annotations *and* be further validated in the controller *and* have its fields adorned with custom attributes. The framework runs the annotations first (short-circuiting on any failure via `[ApiController]`), so by the time your action runs you know the **shape** is right and can focus on the **behaviour** rules.

---

## Where this leads

The API no longer trusts input blindly. Shape rules are declared on DTOs and enforced automatically. Business rules live where they belong — the controllers that own the operation. Reusable domain shape rules are factored out as attributes so we can apply them with a one-line `[HexColor]`.

**Extra Lesson 3** turns to a different problem — URL design. Machines are happy with `/api/Tags/4`, but humans like `/tags/urgent`. We'll auto-generate a `Slug` for every list and tag, enforce uniqueness across the table, and introduce **dependency injection** properly by registering our first custom service — a `Slugifier`.

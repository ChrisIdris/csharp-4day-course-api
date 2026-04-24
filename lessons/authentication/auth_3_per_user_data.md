# Auth Lesson 3 — Scoping every row to its owner

> Start from the end of **Auth Lesson 2**. Copy `AuthLesson2/` to `AuthLesson3/` and rename the project.

Lesson 2 gave us working login. But every authenticated user still sees every list and every tag in the database. This lesson makes the data **personal** — Alice sees Alice's lists, Bob sees Bob's, and neither can even tell the other's exists.

The core move is simple: every `TodoList` and every `Tag` gains an `OwnerId` foreign key to `AspNetUsers`. Every query filters by the current user's Id. Every `[Authorize]`d action reads that Id from the JWT's `sub` claim.

Getting to that simple end-state has three moving parts worth teaching carefully:

1. **Adding `OwnerId` to the entities**, plus the FK and cascade configuration.
2. **Changing slug uniqueness from global to per-user** — Alice and Bob should both be able to have a list with slug `groceries`.
3. **Moving the Inbox out of `HasData`** and into per-user imperative seeding — every user gets their own Inbox.

Plus the controller work: read the user's Id from claims, scope every query, return 404 (not 403) for cross-user access.

## The entities get an `OwnerId`

`TodoList` and `Tag` both gain two new members:

```csharp
public class TodoList : IHasUpdatedAt
{
    // ...existing fields...

    public string OwnerId { get; set; } = string.Empty;

    [JsonIgnore]
    public ApplicationUser? Owner { get; set; }

    public List<Todo>? Todos { get; set; }
}
```

`OwnerId` is a **string** — not an int — because `IdentityUser.Id` is a string (GUID). The two columns have to match type for EF to understand the foreign key.

`Owner` is a navigation property so we *could* write `list.Owner.Email` in C# if we ever wanted to. `[JsonIgnore]` keeps the back-reference out of response bodies.

**What about `Todo`?** No `OwnerId`. Todos inherit their ownership through their parent `TodoList`. This matters because it changes how we scope queries against todos — we filter by `t.TodoList.OwnerId`, which EF Core translates into a JOIN. One more degree of indirection; no new column.

**What about `TodoTag`?** Same story — it inherits through `Todo → TodoList → Owner`.

## The DbContext — FKs, cascade, composite uniqueness

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<TodoTag>()
        .HasKey(tt => new { tt.TodoId, tt.TagId });

    // Slug uniqueness is now COMPOSITE — (OwnerId, Slug) must be unique, not
    // Slug on its own. Alice and Bob can both have a "groceries" slug because
    // their OwnerIds differ.
    modelBuilder.Entity<TodoList>()
        .HasIndex(l => new { l.OwnerId, l.Slug })
        .IsUnique();

    modelBuilder.Entity<Tag>()
        .HasIndex(t => new { t.OwnerId, t.Slug })
        .IsUnique();

    // FK relationships to the user table. Cascade on delete: removing an
    // ApplicationUser also removes their lists and tags. The existing cascade
    // from TodoList to its Todos (from Lesson 5) continues the chain.
    modelBuilder.Entity<TodoList>()
        .HasOne(l => l.Owner)
        .WithMany()
        .HasForeignKey(l => l.OwnerId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Tag>()
        .HasOne(t => t.Owner)
        .WithMany()
        .HasForeignKey(t => t.OwnerId)
        .OnDelete(DeleteBehavior.Cascade);

    // REMOVED — the Inbox HasData seed is gone. An Inbox needs an OwnerId, and
    // OwnerId can't be a compile-time constant. It moves to DbSeeder (below).
}
```

Three changes worth understanding.

**The composite unique index.** `HasIndex(l => new { l.OwnerId, l.Slug }).IsUnique()` follows exactly the pattern from Extra Lesson 1's composite primary key on `TodoTag`. The anonymous-object syntax means "these two columns together are unique" — the same slug can appear multiple times in the table, just not twice with the same owner.

**The `HasOne(...).WithMany().HasForeignKey(...)` config.** This is the fluent way to spell out a relationship EF can't fully infer. `HasOne(l => l.Owner)` says a list has one owner; `.WithMany()` — no argument — says we don't care to wire the *other* side (we're not adding `List<TodoList> OwnedLists` to `ApplicationUser`); `.HasForeignKey(l => l.OwnerId)` says the scalar FK column is `OwnerId`.

**`OnDelete(DeleteBehavior.Cascade)`.** Delete a user, remove their lists and tags. For a todo app this is fine — when the user goes, so does their data. Production apps often prefer soft-deletion; that's a later lesson.

## Seeding — Alice and Bob, each with their own Inbox

The seeder becomes async (because `UserManager` is async) and takes a third argument — `UserManager<ApplicationUser>` — resolved from DI.

```csharp
public static async Task SeedAsync(
    AppDbContext db,
    Slugifier slugifier,
    UserManager<ApplicationUser> userManager)
{
    if (await userManager.FindByEmailAsync("alice@example.com") is not null)
        return;

    var alice = await CreateUserAsync(userManager, "alice@example.com", "Passw0rd!");
    var bob   = await CreateUserAsync(userManager, "bob@example.com",   "Passw0rd!");

    await SeedUserDataAsync(db, slugifier, alice.Id);
    await SeedUserDataAsync(db, slugifier, bob.Id);
}

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
    // ... Groceries, Work tasks, Todos, Tags, TodoTag assignments ...
}
```

The per-user Inbox is the interesting bit. `HasData` handled it neatly in earlier lessons because the row was fixed — same Id, same Title, same everything across every environment. Adding `OwnerId` breaks that assumption: *which* user does the Inbox belong to? Different in every install.

The rule: **`HasData` is for reference data whose primary key is known at build time. Anything that needs a runtime value belongs in the imperative seeder.** Our Inbox now needs each user's Id, and we only know that after `UserManager.CreateAsync`, so it moves.

Update `Program.cs` to call the async seeder:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await DbSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<Slugifier>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
}
```

## Reading the user Id from claims

The canonical way to get the current user's id inside an `[Authorize]`d action:

```csharp
private string GetUserId() =>
    User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated request without a user id claim.");
```

This reads the `sub` claim from the JWT (remapped to `ClaimTypes.NameIdentifier` because we set `NameClaimType` in Lesson 1). `[Authorize]` has already guaranteed a user is authenticated by the time the action runs, so the throw is defensive — it should never fire. If it *does* fire, something is badly wrong with the token configuration and you want to know.

Put this as a private helper on each controller that needs it. Don't refactor it to a base class yet — duplicating a one-liner three times is cheaper than the inheritance it would save.

## Scoping every query

Every controller action that reads or writes user-owned data follows the same three moves:

### Read (GET/list)

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<TodoListResponse>>> GetTodoLists([FromQuery] TodoListQuery query)
{
    var userId = GetUserId();
    var q = _context.TodoLists
        .Where(l => l.OwnerId == userId)
        .AsQueryable();
    // ...rest unchanged...
}
```

`.Where(l => l.OwnerId == userId)` goes into the IQueryable. EF translates it to SQL `WHERE OwnerId = @userId` — the database does the filtering, not C#. No round-trips for rows we'd throw away anyway.

### Read (GET/single)

```csharp
var todoList = await _context.TodoLists
    .Where(l => l.OwnerId == userId)
    .FirstOrDefaultAsync(l => l.Id == id);

if (todoList == null) return NotFound();
```

**The 404 here is important.** If Alice asks for Bob's list by id, she gets `404 Not Found` — *not* `403 Forbidden`. Returning 403 would confirm that a resource exists at that id, which lets an attacker enumerate ids to map out other users' data. 404 says "nothing here for you" and hides whether a row exists at all.

### Write (POST)

```csharp
[HttpPost]
public async Task<ActionResult<TodoListResponse>> PostTodoList(TodoList todoList)
{
    var userId = GetUserId();
    todoList.OwnerId = userId;                    // <- server is authoritative
    todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: null, ownerId: userId);

    _context.TodoLists.Add(todoList);
    await _context.SaveChangesAsync();
    // ...
}
```

Whatever the client sent in the JSON body for `OwnerId`, we overwrite it with the authenticated user's Id. **The server is the authority on ownership.** A malicious client can't POST `{"title":"...", "ownerId":"bob-guid"}` and plant a list in Bob's account.

### Write (PUT/DELETE)

```csharp
var existing = await _context.TodoLists
    .AsNoTracking()
    .FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == userId);
if (existing is null) return NotFound();
```

The existence check and the ownership check are **one query**. If the row doesn't exist OR it belongs to someone else, `FirstOrDefaultAsync` returns null and we return 404. One SQL round-trip, two policies enforced.

## The slug collision loop changes too

Slug uniqueness is now per-owner, so the collision-check loop filters by the current user:

```csharp
private async Task<string> ResolveUniqueSlugAsync(string fromTitle, int? excludeId, string ownerId)
{
    var baseSlug = _slugifier.Slugify(fromTitle);
    var candidate = baseSlug;
    int suffix = 2;
    while (await _context.TodoLists.AnyAsync(l =>
               l.OwnerId == ownerId &&              // <- scoped to this user
               l.Slug == candidate &&
               (excludeId == null || l.Id != excludeId)))
    {
        candidate = $"{baseSlug}-{suffix++}";
    }
    return candidate;
}
```

`TagsController.ResolveUniqueSlugAsync` gets the same treatment. Consequence: Alice creating a second "Groceries" list produces `groceries-2` in *her* slug space; Bob's untouched `groceries` stays exactly that.

## The two-user test

The seeder creates Alice and Bob. Get a token for each:

```bash
ALICE=$(curl -s -X POST http://localhost:5107/api/auth/login \
        -H "Content-Type: application/json" \
        -d '{"email":"alice@example.com","password":"Passw0rd!"}' \
        | jq -r '.accessToken')

BOB=$(curl -s -X POST http://localhost:5107/api/auth/login \
      -H "Content-Type: application/json" \
      -d '{"email":"bob@example.com","password":"Passw0rd!"}' \
      | jq -r '.accessToken')
```

Alice's lists — only Alice's:

```bash
curl -H "Authorization: Bearer $ALICE" http://localhost:5107/api/TodoLists
# → Alice's Inbox, Groceries, Work tasks
```

Bob's lists — only Bob's, different ids:

```bash
curl -H "Authorization: Bearer $BOB" http://localhost:5107/api/TodoLists
# → Bob's Inbox, Groceries, Work tasks (same titles, different ids and owners)
```

Alice tries to read one of Bob's list ids:

```bash
curl -i -H "Authorization: Bearer $ALICE" http://localhost:5107/api/TodoLists/7
# → HTTP 404 Not Found
```

Alice creates a duplicate "Groceries":

```bash
curl -X POST -H "Authorization: Bearer $ALICE" http://localhost:5107/api/TodoLists \
     -H "Content-Type: application/json" \
     -d '{"title":"Groceries","description":"Second one"}'
# → {"id":N,"title":"Groceries","slug":"groceries-2",...}
```

Bob's original `groceries` slug is unaffected — his row was never in Alice's uniqueness scope to begin with.

Alice tries to delete her own Inbox:

```bash
curl -i -X DELETE -H "Authorization: Bearer $ALICE" http://localhost:5107/api/TodoLists/<alice-inbox-id>
# → HTTP 400 — This list is marked as non-deletable and cannot be removed.
```

The `Deletable` guard from the main course still works — it's a per-list business rule, orthogonal to ownership. The Inbox seeded for each user is non-deletable by construction.

## What's coming next

The API is now a real multi-tenant application. Data is isolated per user; tokens scope every request; the Inbox exists per account. A few natural next lessons if you want to keep going:

- **Refresh tokens** — short-lived access tokens + long-lived refresh tokens with proper revocation.
- **Roles and policy-based authorization** — some endpoints only for admins, some only for the resource owner, expressed declaratively.
- **Action filters to DRY the ownership checks** — the `FirstOrDefaultAsync(... && x.OwnerId == userId)` pattern is ripe for extraction once you've seen it in every controller.
- **Soft delete** — `IsDeleted` flag + query filter + compliance win.
- **Per-user rate limiting** on the login endpoint — real apps need this; JWTs alone don't prevent brute force against `/api/auth/login`.

Each is its own focused lesson, and each builds on everything you've seen so far.

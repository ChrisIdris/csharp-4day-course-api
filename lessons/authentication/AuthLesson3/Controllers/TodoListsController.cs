using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthLesson3.Data;
using AuthLesson3.Dtos;
using AuthLesson3.Models;
using AuthLesson3.Services;

namespace AuthLesson3.Controllers
{
    // New in Auth Lesson 3 — [Authorize] protects every action on this controller.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TodoListsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Slugifier _slugifier;

        public TodoListsController(AppDbContext context, Slugifier slugifier)
        {
            _context = context;
            _slugifier = slugifier;
        }

        // Pulls the current user's Id out of the "sub" claim (which we mapped to
        // ClaimTypes.NameIdentifier in Auth Lesson 1). [Authorize] guarantees the
        // claim is present — the throw is defensive, not expected.
        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Authenticated request without a user id claim.");

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoListResponse>>> GetTodoLists([FromQuery] TodoListQuery query)
        {
            var userId = GetUserId();
            // Scoping by owner is part of the query, not a post-load filter.
            // EF translates it to SQL WHERE OwnerId = @userId.
            var q = _context.TodoLists
                .Where(l => l.OwnerId == userId)
                .AsQueryable();

            if (ParseInclude(query.Include).Contains("todos"))
            {
                if (query.Completed.HasValue)
                {
                    bool wantComplete = query.Completed.Value;
                    q = q.Include(l => l.Todos!.Where(t => t.IsComplete == wantComplete));
                }
                else
                {
                    q = q.Include(l => l.Todos);
                }
            }

            var lists = await q.ToListAsync();
            return lists.Select(TodoListResponse.FromEntity).ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TodoListResponse>> GetTodoList(int id, [FromQuery] TodoListQuery query)
        {
            var userId = GetUserId();
            var q = _context.TodoLists
                .Where(l => l.OwnerId == userId)
                .AsQueryable();

            if (ParseInclude(query.Include).Contains("todos"))
            {
                if (query.Completed.HasValue)
                {
                    bool wantComplete = query.Completed.Value;
                    q = q.Include(l => l.Todos!.Where(t => t.IsComplete == wantComplete));
                }
                else
                {
                    q = q.Include(l => l.Todos);
                }
            }

            var todoList = await q.FirstOrDefaultAsync(l => l.Id == id);

            // 404, not 403. Hiding the fact that "list 5 belongs to someone else"
            // prevents a caller from enumerating ids to discover other users' data.
            if (todoList == null)
            {
                return NotFound();
            }

            return TodoListResponse.FromEntity(todoList);
        }

        private static HashSet<string> ParseInclude(string? include) =>
            string.IsNullOrWhiteSpace(include)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : include
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodoList(int id, TodoList todoList)
        {
            if (id != todoList.Id)
            {
                return BadRequest();
            }

            var userId = GetUserId();

            // Verify the row exists AND belongs to the current user in ONE query.
            // If someone tries to PUT another user's list, the row isn't found and
            // we return 404 — same shape as "resource doesn't exist."
            var existing = await _context.TodoLists
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == userId);
            if (existing is null)
            {
                return NotFound();
            }

            // Preserve server-controlled fields: OwnerId and CreatedAt can never be
            // changed by a PUT body. The client is authoritative on Title, Description,
            // Deletable — we're authoritative on everything else.
            todoList.OwnerId = userId;
            todoList.CreatedAt = existing.CreatedAt;
            todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: id, ownerId: userId);

            _context.Entry(todoList).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TodoListExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<TodoListResponse>> PostTodoList(TodoList todoList)
        {
            var userId = GetUserId();

            // Server is authoritative on OwnerId — whatever the client sent is thrown away.
            todoList.OwnerId = userId;
            todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: null, ownerId: userId);

            _context.TodoLists.Add(todoList);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetTodoList),
                new { id = todoList.Id },
                TodoListResponse.FromEntity(todoList));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodoList(int id)
        {
            var userId = GetUserId();

            // Single query for "exists AND mine". Another user's list returns 404.
            var todoList = await _context.TodoLists
                .FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == userId);
            if (todoList == null)
            {
                return NotFound();
            }

            if (!todoList.Deletable)
            {
                return Problem(
                    detail: "This list is marked as non-deletable and cannot be removed.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            _context.TodoLists.Remove(todoList);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Slug uniqueness is now scoped per owner — a slug collision only happens
        // when another of THIS USER's lists already uses the candidate.
        private async Task<string> ResolveUniqueSlugAsync(string fromTitle, int? excludeId, string ownerId)
        {
            var baseSlug = _slugifier.Slugify(fromTitle);
            var candidate = baseSlug;
            int suffix = 2;
            while (await _context.TodoLists.AnyAsync(l =>
                       l.OwnerId == ownerId &&
                       l.Slug == candidate &&
                       (excludeId == null || l.Id != excludeId)))
            {
                candidate = $"{baseSlug}-{suffix++}";
            }
            return candidate;
        }

        private bool TodoListExists(int id)
        {
            return _context.TodoLists.Any(e => e.Id == id);
        }
    }
}

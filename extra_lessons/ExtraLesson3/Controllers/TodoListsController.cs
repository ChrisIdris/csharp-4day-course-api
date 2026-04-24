using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExtraLesson3.Data;
using ExtraLesson3.Dtos;
using ExtraLesson3.Models;
using ExtraLesson3.Services;

namespace ExtraLesson3.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoListsController : ControllerBase
    {
        private readonly AppDbContext _context;

        // New in Extra Lesson 3 — second dependency. DI pulls it from the container
        // and hands it in; the controller doesn't know or care that Slugifier was
        // registered as a Singleton in Program.cs.
        private readonly Slugifier _slugifier;

        public TodoListsController(AppDbContext context, Slugifier slugifier)
        {
            _context = context;
            _slugifier = slugifier;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoListResponse>>> GetTodoLists([FromQuery] TodoListQuery query)
        {
            var q = _context.TodoLists.AsQueryable();

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
            var q = _context.TodoLists.AsQueryable();

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

            // Re-derive the slug from the (possibly updated) title, resolving collisions
            // against every OTHER row. Excluding self (l.Id != id) is important — if the
            // title didn't change, the row's own current slug shouldn't count as a clash.
            todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: id);

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
            // Derive and assign the slug BEFORE saving. Any client-supplied Slug value is
            // ignored — the server is authoritative here. Collision resolution happens in
            // the helper below.
            todoList.Slug = await ResolveUniqueSlugAsync(todoList.Title, excludeId: null);

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
            var todoList = await _context.TodoLists.FindAsync(id);
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

        // Turn a title into a slug that's unique across the TodoLists table. If the base
        // slug is free, use it. Otherwise append "-2", "-3", ... until we find one that
        // isn't taken. The `excludeId` parameter is null on create and the row's own Id
        // on update, so a PUT that doesn't change the title keeps its existing slug.
        private async Task<string> ResolveUniqueSlugAsync(string fromTitle, int? excludeId)
        {
            var baseSlug = _slugifier.Slugify(fromTitle);
            var candidate = baseSlug;
            int suffix = 2;
            while (await _context.TodoLists.AnyAsync(l =>
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

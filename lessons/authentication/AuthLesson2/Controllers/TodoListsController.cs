using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthLesson2.Data;
using AuthLesson2.Dtos;
using AuthLesson2.Models;
using AuthLesson2.Services;

namespace AuthLesson2.Controllers
{
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExtraLesson1.Data;
using ExtraLesson1.Dtos;
using ExtraLesson1.Models;

namespace ExtraLesson1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoListsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TodoListsController(AppDbContext context)
        {
            _context = context;
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

            // Entity → DTO projection. This is the single point where the database shape
            // becomes the wire shape: Deletable disappears, the cycle can't form, and the
            // response is exactly what the API contract promises.
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
            _context.TodoLists.Add(todoList);
            await _context.SaveChangesAsync();

            // Return the DTO, not the entity. CreatedAtAction's third argument becomes
            // the response body.
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

        private bool TodoListExists(int id)
        {
            return _context.TodoLists.Any(e => e.Id == id);
        }
    }
}

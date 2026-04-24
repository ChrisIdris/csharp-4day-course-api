using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lesson7.Data;
using Lesson7.Models;

namespace Lesson7.Controllers
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

        // GET: api/TodoLists?include=todos&completed=false
        //
        // Both list endpoints accept the same TodoListQuery knobs — ASP.NET binds each
        // public property from the matching query-string key (case-insensitive).
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoList>>> GetTodoLists([FromQuery] TodoListQuery query)
        {
            var q = _context.TodoLists.AsQueryable();

            if (ParseInclude(query.Include).Contains("todos"))
            {
                if (query.Completed.HasValue)
                {
                    // Filtered Include (EF Core 5+) narrows the joined rows at the SQL layer
                    // instead of loading everything and filtering in memory.
                    //
                    // query.Completed is `bool?` — it can be true, false, OR null. Inside this
                    // branch we already know it has a value, so we pull it into a plain `bool`
                    // called `wantComplete` ("the completion state the caller asked for").
                    // That keeps the Where-lambda readable: `t.IsComplete == wantComplete` reads
                    // as "this todo's complete-state matches what we want".
                    bool wantComplete = query.Completed.Value;
                    q = q.Include(l => l.Todos!.Where(t => t.IsComplete == wantComplete));
                }
                else
                {
                    q = q.Include(l => l.Todos);
                }
            }

            return await q.ToListAsync();
        }

        // GET: api/TodoLists/5?include=todos&completed=true
        //
        // Lesson 6 unconditionally eager-loaded Todos. Lesson 7 makes that opt-in:
        // requests pay for exactly what they asked for.
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoList>> GetTodoList(int id, [FromQuery] TodoListQuery query)
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

            return todoList;
        }

        // Parses `?include=todos,owner` into a case-insensitive set. Returns an empty set
        // when the parameter is absent, so callers can use `.Contains("todos")` unconditionally.
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
        public async Task<ActionResult<TodoList>> PostTodoList(TodoList todoList)
        {
            _context.TodoLists.Add(todoList);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTodoList", new { id = todoList.Id }, todoList);
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

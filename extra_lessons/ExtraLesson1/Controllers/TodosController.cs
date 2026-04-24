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
    public class TodosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TodosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Todos
        //
        // New in Extra Lesson 1: we .Include the TodoTags join AND .ThenInclude the Tag
        // entity so the response DTO can read the tag's Name and default Color. Without
        // ThenInclude, TodoTag.Tag stays null and TodoTagResponse.FromEntity throws.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoResponse>>> GetTodos([FromQuery] bool? completed)
        {
            var query = _context.Todos
                .Include(t => t.TodoTags!)
                    .ThenInclude(tt => tt.Tag)
                .AsQueryable();

            if (completed is not null)
            {
                query = query.Where(t => t.IsComplete == completed.Value);
            }

            var todos = await query.ToListAsync();
            return todos.Select(TodoResponse.FromEntity).ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TodoResponse>> GetTodo(int id)
        {
            var todo = await _context.Todos
                .Include(t => t.TodoTags!)
                    .ThenInclude(tt => tt.Tag)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (todo == null)
            {
                return NotFound();
            }

            return TodoResponse.FromEntity(todo);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodo(int id, Todo todo)
        {
            if (id != todo.Id)
            {
                return BadRequest();
            }

            _context.Entry(todo).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TodoExists(id))
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
        public async Task<ActionResult<TodoResponse>> PostTodo(Todo todo)
        {
            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, TodoResponse.FromEntity(todo));
        }

        [HttpPost("with-new-list")]
        public async Task<ActionResult<TodoResponse>> CreateWithNewList(CreateTodoWithNewListRequest request)
        {
            var list = new TodoList
            {
                Title = request.ListTitle,
                Description = request.ListDescription
            };
            _context.TodoLists.Add(list);
            await _context.SaveChangesAsync();

            var todo = new Todo
            {
                Title = request.TodoTitle,
                IsComplete = request.IsComplete,
                TodoListId = list.Id
            };
            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, TodoResponse.FromEntity(todo));
        }

        // POST: api/Todos/{id}/tags
        //
        // Attaches an existing Tag to an existing Todo, optionally overriding the tag's
        // default colour for this assignment. Returns 201 with the created TodoTagResponse.
        //
        // Why a sub-route? The created resource is one row in the TodoTag join — it has
        // meaning only *within* the context of its parent Todo. Nesting the URL under
        // /api/Todos/{id}/ reflects that containment.
        [HttpPost("{id}/tags")]
        public async Task<ActionResult<TodoTagResponse>> AttachTag(int id, AttachTagRequest request)
        {
            var todoExists = await _context.Todos.AnyAsync(t => t.Id == id);
            if (!todoExists)
            {
                return NotFound();
            }

            var tag = await _context.Tags.FindAsync(request.TagId);
            if (tag is null)
            {
                return Problem(
                    detail: $"Tag with id {request.TagId} does not exist.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // The composite PK enforces "one TodoTag row per (todo, tag)" at the DB level —
            // but a friendlier 409 is nicer than letting EF throw on SaveChanges.
            var already = await _context.TodoTags.FindAsync(id, request.TagId);
            if (already is not null)
            {
                return Problem(
                    detail: "This tag is already attached to this todo.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var tt = new TodoTag
            {
                TodoId = id,
                TagId = request.TagId,
                ColorOverride = request.ColorOverride
            };
            _context.TodoTags.Add(tt);
            await _context.SaveChangesAsync();

            // We already have the Tag loaded from the check above, so hydrate the nav
            // before mapping — TodoTagResponse.FromEntity needs it.
            tt.Tag = tag;
            return TodoTagResponse.FromEntity(tt);
        }

        // DELETE: api/Todos/{id}/tags/{tagId}
        //
        // Detaches a tag from a todo (removes the TodoTag row). The Tag itself continues
        // to exist; only the assignment is removed. 404 if the assignment didn't exist.
        [HttpDelete("{id}/tags/{tagId}")]
        public async Task<IActionResult> DetachTag(int id, int tagId)
        {
            var tt = await _context.TodoTags.FindAsync(id, tagId);
            if (tt is null)
            {
                return NotFound();
            }

            _context.TodoTags.Remove(tt);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodo(int id)
        {
            var todo = await _context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            _context.Todos.Remove(todo);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TodoExists(int id)
        {
            return _context.Todos.Any(e => e.Id == id);
        }
    }
}

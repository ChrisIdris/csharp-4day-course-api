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
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TodosController : ControllerBase
    {
        private const int MaxTagsPerTodo = 5;

        private readonly AppDbContext _context;
        private readonly Slugifier _slugifier;

        public TodosController(AppDbContext context, Slugifier slugifier)
        {
            _context = context;
            _slugifier = slugifier;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Authenticated request without a user id claim.");

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoResponse>>> GetTodos([FromQuery] bool? completed)
        {
            var userId = GetUserId();

            // Todos have no OwnerId column — ownership inherits through their
            // parent TodoList. Scoping by TodoList.OwnerId makes EF emit a JOIN
            // in SQL, so the DB does the filtering without loading everything.
            var query = _context.Todos
                .Where(t => t.TodoList!.OwnerId == userId)
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
            var userId = GetUserId();
            var todo = await _context.Todos
                .Where(t => t.TodoList!.OwnerId == userId)
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

            var userId = GetUserId();
            var existing = await _context.Todos
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.TodoList!.OwnerId == userId);
            if (existing is null)
            {
                return NotFound();
            }

            // The caller can't reparent a todo into someone else's list — they
            // can only reparent between their own lists. Confirm the target
            // list is theirs.
            var targetListOwnedByUser = await _context.TodoLists
                .AnyAsync(l => l.Id == todo.TodoListId && l.OwnerId == userId);
            if (!targetListOwnedByUser)
            {
                return Problem(
                    detail: $"TodoListId {todo.TodoListId} does not exist or does not belong to you.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            todo.CreatedAt = existing.CreatedAt;
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
            var userId = GetUserId();

            var listOwnedByUser = await _context.TodoLists
                .AnyAsync(l => l.Id == todo.TodoListId && l.OwnerId == userId);
            if (!listOwnedByUser)
            {
                return Problem(
                    detail: $"TodoListId {todo.TodoListId} does not exist or does not belong to you.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, TodoResponse.FromEntity(todo));
        }

        [HttpPost("with-new-list")]
        public async Task<ActionResult<TodoResponse>> CreateWithNewList(CreateTodoWithNewListRequest request)
        {
            var userId = GetUserId();

            var list = new TodoList
            {
                OwnerId = userId,
                Title = request.ListTitle,
                Slug = _slugifier.Slugify(request.ListTitle),
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

        [HttpPost("{id}/tags")]
        public async Task<ActionResult<TodoTagResponse>> AttachTag(int id, AttachTagRequest request)
        {
            var userId = GetUserId();

            // Both the todo AND the tag must belong to the current user.
            var todoOwned = await _context.Todos
                .AnyAsync(t => t.Id == id && t.TodoList!.OwnerId == userId);
            if (!todoOwned)
            {
                return NotFound();
            }

            var existingCount = await _context.TodoTags.CountAsync(tt => tt.TodoId == id);
            if (existingCount >= MaxTagsPerTodo)
            {
                return Problem(
                    detail: $"A todo can have at most {MaxTagsPerTodo} tags; this one already has {existingCount}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var tag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Id == request.TagId && t.OwnerId == userId);
            if (tag is null)
            {
                return Problem(
                    detail: $"Tag with id {request.TagId} does not exist.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

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

            tt.Tag = tag;
            return TodoTagResponse.FromEntity(tt);
        }

        [HttpDelete("{id}/tags/{tagId}")]
        public async Task<IActionResult> DetachTag(int id, int tagId)
        {
            var userId = GetUserId();

            var tt = await _context.TodoTags
                .Where(x => x.Todo!.TodoList!.OwnerId == userId)
                .FirstOrDefaultAsync(x => x.TodoId == id && x.TagId == tagId);
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
            var userId = GetUserId();

            var todo = await _context.Todos
                .Where(t => t.TodoList!.OwnerId == userId)
                .FirstOrDefaultAsync(t => t.Id == id);
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

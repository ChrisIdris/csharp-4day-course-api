using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lesson8.Data;
using Lesson8.Dtos;
using Lesson8.Models;

namespace Lesson8.Controllers
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoResponse>>> GetTodos([FromQuery] bool? completed)
        {
            var query = _context.Todos.AsQueryable();

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
            var todo = await _context.Todos.FindAsync(id);

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

        // POST: api/Todos/with-new-list
        //
        // Creates a new TodoList AND a new Todo inside it in a single call. The request
        // body doesn't map to any single entity — it describes the OPERATION, which is
        // what request DTOs are for. See Dtos/CreateTodoWithNewListRequest.cs.
        //
        // Two SaveChanges calls: the list's Id isn't assigned until after its first save,
        // and we need that Id on the Todo's TodoListId. Same two-phase pattern as Lesson
        // 5's DbSeeder — real transactional atomicity is a story for a later lesson.
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

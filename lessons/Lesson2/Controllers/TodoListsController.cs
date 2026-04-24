using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lesson2.Data;
using Lesson2.Models;

namespace Lesson2.Controllers
{
    // [Route(...)] defines the URL prefix for every action in this controller.
    // The literal "[controller]" is a token that ASP.NET replaces with the controller
    // class name minus the "Controller" suffix. So TodoListsController becomes the
    // URL segment "TodoLists", and the full route for actions below is "api/TodoLists".
    [Route("api/[controller]")]
    // [ApiController] opts this class into a bundle of Web API conventions:
    //   - automatic HTTP 400 responses when model binding fails
    //   - parameters bound from the request body/route/query without needing [FromBody] etc. in simple cases
    //   - problem-details (RFC 7807) error responses
    // If you forget this attribute, your controller still works but you'll write more boilerplate.
    [ApiController]
    public class TodoListsController : ControllerBase
    {
        // The DbContext is our gateway to the database. We store it in a private field
        // so every action can query/save through _context.
        private readonly AppDbContext _context;

        // Constructor injection: ASP.NET's dependency injection container sees that
        // this controller needs an AppDbContext and supplies one automatically,
        // because we registered AppDbContext in Program.cs with AddDbContext<AppDbContext>(...).
        // Each HTTP request gets its own scoped DbContext instance.
        public TodoListsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/TodoLists
        //
        // [HttpGet] with no arguments matches GET requests to the controller's base route.
        // ActionResult<IEnumerable<TodoList>> means: "I return either an HTTP result
        // (NotFound, BadRequest, etc.) or a collection of TodoList that ASP.NET will
        // serialize to JSON automatically."
        // The action is async because EF Core I/O is async — never block a thread waiting on a DB.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoList>>> GetTodoLists()
        {
            // ToListAsync materialises the query into an in-memory list and returns it.
            // ASP.NET wraps this in a 200 OK response with a JSON body.
            return await _context.TodoLists.ToListAsync();
        }

        // GET: api/TodoLists/5
        //
        // The "{id}" segment in the route template is a route parameter.
        // ASP.NET binds it to the int id method parameter automatically.
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoList>> GetTodoList(int id)
        {
            // FindAsync looks up an entity by its primary key. Returns null if not found.
            // (It's more efficient than a LINQ Where + FirstOrDefault because it checks
            // the DbContext's change tracker first before hitting the database.)
            var todoList = await _context.TodoLists.FindAsync(id);

            if (todoList == null)
            {
                // NotFound() produces an HTTP 404 response with no body.
                return NotFound();
            }

            // Returning the entity directly produces HTTP 200 with the JSON-serialized body.
            return todoList;
        }

        // PUT: api/TodoLists/5
        //
        // PUT replaces an entire resource. The client sends the full updated TodoList
        // in the request body, and the {id} in the URL must match todoList.Id.
        //
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodoList(int id, TodoList todoList)
        {
            // Sanity check: the URL id and the body id must refer to the same resource.
            if (id != todoList.Id)
            {
                return BadRequest();
            }

            // Tell EF Core: "this entity is in the Modified state — generate an UPDATE for it."
            _context.Entry(todoList).State = EntityState.Modified;

            try
            {
                // Flushes pending changes to the database.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // This exception is thrown when the row being updated no longer exists
                // (for example, another request deleted it between our load and save).
                // We distinguish "row missing" (404) from a real concurrency conflict (rethrow).
                if (!TodoListExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // 204 No Content — the update succeeded, and there's no meaningful body to send back.
            return NoContent();
        }

        // POST: api/TodoLists
        //
        // POST creates a new resource. The client sends a TodoList WITHOUT an Id (the DB
        // assigns it), and we echo the created entity back along with a Location header
        // pointing to the new resource.
        //
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TodoList>> PostTodoList(TodoList todoList)
        {
            // Mark the new entity as Added in the change tracker.
            _context.TodoLists.Add(todoList);
            // Persist — this is when EF assigns the generated Id.
            await _context.SaveChangesAsync();

            // CreatedAtAction returns HTTP 201 Created with:
            //   - a Location header like "api/TodoLists/42" built from the GetTodoList action
            //   - the newly created entity as the body
            return CreatedAtAction("GetTodoList", new { id = todoList.Id }, todoList);
        }

        // DELETE: api/TodoLists/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodoList(int id)
        {
            // Load the entity so EF knows what to delete.
            var todoList = await _context.TodoLists.FindAsync(id);
            if (todoList == null)
            {
                return NotFound();
            }

            // Business rule: some lists (e.g. a user's Inbox) are flagged non-deletable.
            // We return 400 Bad Request with an RFC-7807 "problem details" payload — the
            // standard ASP.NET shape for returning an error with a human-readable message.
            if (!todoList.Deletable)
            {
                return Problem(
                    detail: "This list is marked as non-deletable and cannot be removed.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Mark as Deleted and persist.
            _context.TodoLists.Remove(todoList);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Small helper used by PutTodoList to distinguish "row disappeared" from real concurrency conflicts.
        private bool TodoListExists(int id)
        {
            return _context.TodoLists.Any(e => e.Id == id);
        }
    }
}

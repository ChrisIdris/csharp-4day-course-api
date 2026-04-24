using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthLesson1.Data;
using AuthLesson1.Dtos;
using AuthLesson1.Models;
using AuthLesson1.Services;
using Microsoft.AspNetCore.Authorization;

namespace AuthLesson1.Controllers
{
    // New in Auth Lesson 1 — [Authorize] at the class level. Every action on this
    // controller now rejects unauthenticated requests with 401. Students can prove
    // the auth pipeline works by comparing TagsController (protected) against
    // TodoListsController (still open) before we lock everything down in Lesson 3.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Slugifier _slugifier;

        public TagsController(AppDbContext context, Slugifier slugifier)
        {
            _context = context;
            _slugifier = slugifier;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TagResponse>>> GetTags()
        {
            var tags = await _context.Tags.ToListAsync();
            return tags.Select(TagResponse.FromEntity).ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TagResponse>> GetTag(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
            {
                return NotFound();
            }
            return TagResponse.FromEntity(tag);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTag(int id, Tag tag)
        {
            if (id != tag.Id)
            {
                return BadRequest();
            }

            tag.Slug = await ResolveUniqueSlugAsync(tag.Name, excludeId: id);

            _context.Entry(tag).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TagExists(id))
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
        public async Task<ActionResult<TagResponse>> PostTag(CreateTagRequest request)
        {
            var tag = new Tag
            {
                Name = request.Name,
                Slug = await ResolveUniqueSlugAsync(request.Name, excludeId: null),
                Color = request.Color
            };
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, TagResponse.FromEntity(tag));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
            {
                return NotFound();
            }

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<string> ResolveUniqueSlugAsync(string fromName, int? excludeId)
        {
            var baseSlug = _slugifier.Slugify(fromName);
            var candidate = baseSlug;
            int suffix = 2;
            while (await _context.Tags.AnyAsync(t =>
                       t.Slug == candidate &&
                       (excludeId == null || t.Id != excludeId)))
            {
                candidate = $"{baseSlug}-{suffix++}";
            }
            return candidate;
        }

        private bool TagExists(int id)
        {
            return _context.Tags.Any(e => e.Id == id);
        }
    }
}

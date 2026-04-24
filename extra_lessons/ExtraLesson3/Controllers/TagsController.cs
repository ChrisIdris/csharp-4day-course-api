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

        // Same pattern as TodoListsController.ResolveUniqueSlugAsync — deliberately
        // duplicated rather than extracted. Once students have seen the shape twice,
        // pulling it into a shared helper (maybe on Slugifier itself, with a
        // Func<string, Task<bool>> "exists" predicate) is a fine next step.
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

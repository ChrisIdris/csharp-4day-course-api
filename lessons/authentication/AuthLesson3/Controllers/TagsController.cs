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
    public class TagsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Slugifier _slugifier;

        public TagsController(AppDbContext context, Slugifier slugifier)
        {
            _context = context;
            _slugifier = slugifier;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Authenticated request without a user id claim.");

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TagResponse>>> GetTags()
        {
            var userId = GetUserId();
            var tags = await _context.Tags
                .Where(t => t.OwnerId == userId)
                .ToListAsync();
            return tags.Select(TagResponse.FromEntity).ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TagResponse>> GetTag(int id)
        {
            var userId = GetUserId();
            var tag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
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

            var userId = GetUserId();
            var existing = await _context.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
            if (existing is null)
            {
                return NotFound();
            }

            tag.OwnerId = userId;
            tag.CreatedAt = existing.CreatedAt;
            tag.Slug = await ResolveUniqueSlugAsync(tag.Name, excludeId: id, ownerId: userId);

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
            var userId = GetUserId();
            var tag = new Tag
            {
                OwnerId = userId,
                Name = request.Name,
                Slug = await ResolveUniqueSlugAsync(request.Name, excludeId: null, ownerId: userId),
                Color = request.Color
            };
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, TagResponse.FromEntity(tag));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var userId = GetUserId();
            var tag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
            if (tag == null)
            {
                return NotFound();
            }

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<string> ResolveUniqueSlugAsync(string fromName, int? excludeId, string ownerId)
        {
            var baseSlug = _slugifier.Slugify(fromName);
            var candidate = baseSlug;
            int suffix = 2;
            while (await _context.Tags.AnyAsync(t =>
                       t.OwnerId == ownerId &&
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class TagsController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public TagsController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
 
    // Tags List
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
 
        var tags = await _context.Tags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TagListViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                NoteCount = t.NoteTags.Count()
            })
            .ToListAsync();
 
        return View(tags);
    }
 
    // Create Tag
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTagViewModel model)
    {
        var userId = _userManager.GetUserId(User);
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Tag name is required.";
            return RedirectToAction("Index");
        }
 
        var trimmedName = model.Name.Trim();
 
        var exists = await _context.Tags
            .AnyAsync(t => t.UserId == userId && t.Name == trimmedName);
 
        if (exists)
        {
            TempData["Error"] = $"A tag named \"{trimmedName}\" already exists.";
            return RedirectToAction("Index");
        }
 
        var tag = new Tag
        {
            Name = trimmedName,
            Color = model.Color,
            UserId = userId!
        };
 
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Tag \"{tag.Name}\" created.";
        return RedirectToAction("Index");
    }
 
    // Edit Tag
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditTagViewModel model)
    {
        var userId = _userManager.GetUserId(User);
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Tag name is required.";
            return RedirectToAction("Index");
        }
 
        var tag = await _context.Tags
            .FirstOrDefaultAsync(t => t.Id == model.Id && t.UserId == userId);
 
        if (tag == null) return NotFound();
 
        var trimmedName = model.Name.Trim();
 
        // Only check uniqueness if the name is actually changing
        if (tag.Name != trimmedName)
        {
            var exists = await _context.Tags
                .AnyAsync(t => t.UserId == userId && t.Name == trimmedName && t.Id != tag.Id);
 
            if (exists)
            {
                TempData["Error"] = $"A tag named \"{trimmedName}\" already exists.";
                return RedirectToAction("Index");
            }
        }
 
        tag.Name = trimmedName;
        tag.Color = model.Color;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Tag updated.";
        return RedirectToAction("Index");
    }
 
    // Delete Tag
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var tag = await _context.Tags
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
 
        if (tag == null) return NotFound();
 
        // Cascade delete removes all NoteTag associations automatically
        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Tag \"{tag.Name}\" deleted.";
        return RedirectToAction("Index");
    }
    
    // Tag Details (filtered notes)
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var tag = await _context.Tags
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
 
        if (tag == null) return NotFound();
 
        var notes = await _context.Notes
            .Where(n => n.UserId == userId && n.NoteTags.Any(nt => nt.TagId == id))
            .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteListViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                Tags = n.NoteTags
                    .Where(nt=>nt.TagId !=id)
                    .Select(nt=> new TagListViewModel
                    {
                        Id = nt.Tag.Id,
                        Name = nt.Tag.Name,
                        Color = nt.Tag.Color
                    })
                    .OrderBy(t=>t.Name)
                    .ToList()
            })
            .ToListAsync();
 
        var model = new TagDetailsViewModel
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            Notes = notes
        };
 
        return View(model);
    }
    
    // Create Tag (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAjax([FromForm] CreateTagViewModel model)
    {
        var userId = _userManager.GetUserId(User);

        if (!ModelState.IsValid)
        {
            return Json(new { success = false, error = "Tag name is required." });
        }

        var trimmedName = model.Name.Trim();

        var exists = await _context.Tags
            .AnyAsync(t => t.UserId == userId && t.Name == trimmedName);

        if (exists)
        {
            return Json(new { success = false, error = $"A tag named \"{trimmedName}\" already exists." });
        }

        var tag = new Tag
        {
            Name = trimmedName,
            Color = model.Color,
            UserId = userId!
        };

        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            tag = new { id = tag.Id, name = tag.Name, color = tag.Color }
        });
    }
    
    [HttpGet]
    public async Task<IActionResult> ListJson()
    {
        var userId = _userManager.GetUserId(User);
        var tags = await _context.Tags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new { id = t.Id, name = t.Name, color = t.Color })
            .ToListAsync();
        return Json(tags);
    }
}
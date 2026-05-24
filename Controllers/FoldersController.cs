using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;

namespace NoteVault.Controllers;

[Authorize]
public class FoldersController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    
    public FoldersController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
 
        var folders = await _context.Folders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => new FolderListViewModel
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                NoteCount = f.Notes.Count(),
                PileCount = f.Piles.Count(),
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();
 
        return View(folders);
    }
    
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateFolderViewModel());
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFolderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
 
        var userId = _userManager.GetUserId(User);
 
        // Get the next sort order
        var maxSortOrder = await _context.Folders
            .Where(f => f.UserId == userId)
            .MaxAsync(f => (int?)f.SortOrder) ?? 0;
 
        var folder = new Folder
        {
            Name = model.Name,
            Description = model.Description,
            UserId = userId!,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
 
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
 
        return RedirectToAction("Index");
    }
    
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        var model = new EditFolderViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description
        };
 
        return View(model);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditFolderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
 
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == model.Id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        folder.Name = model.Name;
        folder.Description = model.Description;
        folder.UpdatedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        return RedirectToAction("Index");
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .Include(f => f.Piles)
            .Include(f => f.Notes)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        var now = DateTime.UtcNow;
 
        // Soft delete all notes in this folder
        foreach (var note in folder.Notes)
        {
            note.DeletedAt = now;
        }
 
        // Soft delete all piles in this folder
        foreach (var pile in folder.Piles)
        {
            pile.DeletedAt = now;
        }
 
        // Soft delete the folder itself
        folder.DeletedAt = now;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Folder \"{folder.Name}\" and its contents moved to trash.";
        return RedirectToAction("Index");
    }
}
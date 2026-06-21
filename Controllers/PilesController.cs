using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class PilesController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public PilesController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
 
    // ========= Create Pile =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePileViewModel model)
    {
        var userId = _userManager.GetUserId(User);
 
        // Verify the folder belongs to the user
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == model.FolderId && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Pile name is required.";
            return RedirectToAction("Details", "Folders", new { id = model.FolderId });
        }
 
        var maxSortOrder = await _context.Piles
            .Where(p => p.FolderId == model.FolderId)
            .MaxAsync(p => (int?)p.SortOrder) ?? 0;
 
        var pile = new Pile
        {
            Name = model.Name,
            FolderId = model.FolderId,
            Color = model.Color,
            Icon = model.Icon,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
 
        _context.Piles.Add(pile);
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Pile \"{pile.Name}\" created.";
        return RedirectToAction("Details", "Folders", new { id = model.FolderId });
    }
 
    // ========= Edit Pile =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditPileViewModel model)
    {
        var userId = _userManager.GetUserId(User);
 
        var pile = await _context.Piles
            .Include(p => p.Folder)
            .FirstOrDefaultAsync(p => p.Id == model.Id && p.Folder.UserId == userId);
 
        if (pile == null) return NotFound();
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Pile name is required.";
            return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
        }
 
        pile.Name = model.Name;
        pile.Color = model.Color;
        pile.Icon = model.Icon;
        pile.UpdatedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Pile updated.";
        return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
    }
 
    // ========= Toggle Pin =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var pile = await _context.Piles
            .Include(p => p.Folder)
            .FirstOrDefaultAsync(p => p.Id == id && p.Folder.UserId == userId);
 
        if (pile == null) return NotFound();
 
        pile.IsPinned = !pile.IsPinned;
        pile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
 
        TempData["Success"] = pile.IsPinned ? "Pile pinned." : "Pile unpinned.";
        return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
    }
 
    // ========= Manage Notes =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageNotes(ManageNotesRequest request)
    {
        var userId = _userManager.GetUserId(User);
 
        var pile = await _context.Piles
            .Include(p => p.Folder)
            .Include(p => p.PileNotes)
            .FirstOrDefaultAsync(p => p.Id == request.PileId && p.Folder.UserId == userId);
 
        if (pile == null) return NotFound();
 
        // Get all candidate notes — must be in the same folder
        var folderNoteIds = await _context.Notes
            .Where(n => n.FolderId == pile.FolderId && n.UserId == userId)
            .Select(n => n.Id)
            .ToListAsync();
 
        // Filter requested IDs to only those in this folder
        var validIds = request.NoteIds
            .Where(id => folderNoteIds.Contains(id))
            .ToHashSet();
 
        // Current note IDs in this pile
        var currentIds = pile.PileNotes.Select(pn => pn.NoteId).ToHashSet();
 
        // Remove notes that were unchecked
        var toRemove = pile.PileNotes.Where(pn => !validIds.Contains(pn.NoteId)).ToList();
        _context.PileNotes.RemoveRange(toRemove);
 
        // Add new notes (and remove them from any other pile in the same folder)
        var toAdd = validIds.Except(currentIds).ToList();
        if (toAdd.Any())
        {
            // Find other pile associations for these notes within the same folder
            var crossPileAssociations = await _context.PileNotes
                .Where(pn => toAdd.Contains(pn.NoteId) && pn.Pile.FolderId == pile.FolderId && pn.PileId != pile.Id)
                .ToListAsync();
            _context.PileNotes.RemoveRange(crossPileAssociations);
 
            var maxSortOrder = pile.PileNotes.Any() ? pile.PileNotes.Max(pn => pn.SortOrder) : 0;
            foreach (var noteId in toAdd)
            {
                maxSortOrder++;
                _context.PileNotes.Add(new PileNote
                {
                    PileId = pile.Id,
                    NoteId = noteId,
                    SortOrder = maxSortOrder
                });
            }
        }
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Pile notes updated.";
        return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
    }
 
    // ========= Reorder Pile (up/down) =========
    public class SavePileOrderRequest
    {
        public int FolderId { get; set; }
        public List<int> PileIds { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> SavePileOrder([FromBody] SavePileOrderRequest request)
    {
        var userId = _userManager.GetUserId(User);

        var piles = await _context.Piles
            .Include(p => p.Folder)
            .Where(p => p.FolderId == request.FolderId && p.Folder.UserId == userId)
            .ToListAsync();

        if (!piles.Any()) return NotFound();

        for (int i = 0; i < request.PileIds.Count; i++)
        {
            var pile = piles.FirstOrDefault(p => p.Id == request.PileIds[i]);
            if (pile != null) pile.SortOrder = i + 1;
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
 
    // ========= Reorder Note in Pile =========
    public class SaveNoteOrderRequest
    {
        public int PileId { get; set; }
        public List<int> NoteIds { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> SaveNoteOrder([FromBody] SaveNoteOrderRequest request)
    {
        var userId = _userManager.GetUserId(User);

        var pile = await _context.Piles
            .Include(p => p.Folder)
            .Include(p => p.PileNotes)
            .FirstOrDefaultAsync(p => p.Id == request.PileId && p.Folder.UserId == userId);

        if (pile == null) return NotFound();

        for (int i = 0; i < request.NoteIds.Count; i++)
        {
            var pn = pile.PileNotes.FirstOrDefault(p => p.NoteId == request.NoteIds[i]);
            if (pn != null) pn.SortOrder = i + 1;
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
 
    // ========= Soft Delete Pile =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var pile = await _context.Piles
            .Include(p => p.Folder)
            .Include(p => p.PileNotes)
            .FirstOrDefaultAsync(p => p.Id == id && p.Folder.UserId == userId);
 
        if (pile == null) return NotFound();
 
        var folderId = pile.FolderId;
 
        // Soft delete the pile
        pile.DeletedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Pile \"{pile.Name}\" moved to trash.";
        return RedirectToAction("Details", "Folders", new { id = folderId });
    }
}
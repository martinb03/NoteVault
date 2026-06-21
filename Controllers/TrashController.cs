using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class TrashController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public TrashController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
 
    // ══════════════ Index — Tabbed Trash View ═══════════
    [HttpGet]
    public async Task<IActionResult> Index(string tab = "folders")
    {
        var userId = _userManager.GetUserId(User);
        var model = new TrashViewModel { Tab = tab };
 
        // ── Folders tab data ──
        var deletedFolders = await _context.Folders
            .IgnoreQueryFilters()
            .Where(f => f.UserId == userId && f.DeletedAt != null)
            .Include(f => f.Piles)
            .Include(f => f.Notes)
            .OrderByDescending(f => f.DeletedAt)
            .ToListAsync();
 
        model.Folders = deletedFolders.Select(f => new TrashFolderDto
        {
            Id = f.Id,
            Name = f.Name,
            DeletedAt = f.DeletedAt!.Value,
            Piles = f.Piles
                .Where(p => p.DeletedAt != null && p.DeletedAt >= f.DeletedAt)
                .OrderBy(p => p.Name)
                .Select(p => new TrashFolderPileDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Color = p.Color,
                    Icon = p.Icon
                })
                .ToList(),
            Notes = f.Notes
                .Where(n => n.DeletedAt != null && n.DeletedAt >= f.DeletedAt)
                .OrderBy(n => n.Title)
                .Select(n => new TrashFolderNoteDto
                {
                    Id = n.Id,
                    Title = n.Title
                })
                .ToList()
        }).ToList();
 
        // ── Piles tab data ──
        // Piles directly deleted by user: either folder is alive, or pile was deleted before its folder
        var deletedPiles = await _context.Piles
            .IgnoreQueryFilters()
            .Include(p => p.Folder)
            .Where(p => p.Folder.UserId == userId && p.DeletedAt != null)
            .Where(p => p.Folder.DeletedAt == null || p.DeletedAt < p.Folder.DeletedAt)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync();
 
        model.Piles = deletedPiles.Select(p => new TrashPileDto
        {
            Id = p.Id,
            Name = p.Name,
            Color = p.Color,
            Icon = p.Icon,
            FolderName = p.Folder.Name,
            ParentFolderDeleted = p.Folder.DeletedAt != null,
            DeletedAt = p.DeletedAt!.Value
        }).ToList();
 
        // ── Notes tab data ──
        // Notes directly deleted: unfiled, or folder still alive, or note deleted before folder
        var deletedNotes = await _context.Notes
            .IgnoreQueryFilters()
            .Include(n => n.Folder)
            .Where(n => n.UserId == userId && n.DeletedAt != null)
            .Where(n => n.FolderId == null
                        || n.Folder!.DeletedAt == null
                        || n.DeletedAt < n.Folder.DeletedAt)
            .OrderByDescending(n => n.DeletedAt)
            .ToListAsync();
 
        model.Notes = deletedNotes.Select(n => new TrashNoteDto
        {
            Id = n.Id,
            Title = n.Title,
            FolderName = n.Folder?.DeletedAt == null ? n.Folder?.Name : null,
            DeletedAt = n.DeletedAt!.Value
        }).ToList();
 
        return View(model);
    }
 
    // ══════════════ Restore ═════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(
        List<int>? folderIds,
        List<int>? pileIds,
        List<int>? noteIds,
        string tab = "folders")
    {
        var userId = _userManager.GetUserId(User);
 
        folderIds ??= new List<int>();
        pileIds ??= new List<int>();
        noteIds ??= new List<int>();
 
        // Restore folders
        if (folderIds.Any())
        {
            var folders = await _context.Folders
                .IgnoreQueryFilters()
                .Where(f => folderIds.Contains(f.Id) && f.UserId == userId)
                .ToListAsync();
 
            foreach (var folder in folders)
            {
                folder.DeletedAt = null;
            }
        }
 
        // Restore piles (only if their parent folder is alive after the folder restoration above)
        if (pileIds.Any())
        {
            var piles = await _context.Piles
                .IgnoreQueryFilters()
                .Include(p => p.Folder)
                .Where(p => pileIds.Contains(p.Id) && p.Folder.UserId == userId)
                .ToListAsync();
 
            foreach (var pile in piles)
            {
                // Skip if parent folder is still deleted
                if (pile.Folder.DeletedAt != null)
                {
                    continue;
                }
                pile.DeletedAt = null;
            }
        }
 
        // Restore notes — unfile if their folder is still deleted
        if (noteIds.Any())
        {
            var notes = await _context.Notes
                .IgnoreQueryFilters()
                .Include(n => n.Folder)
                .Where(n => noteIds.Contains(n.Id) && n.UserId == userId)
                .ToListAsync();
 
            foreach (var note in notes)
            {
                if (note.Folder != null && note.Folder.DeletedAt != null)
                {
                    note.FolderId = null;
                }
                note.DeletedAt = null;
            }
        }
 
        await _context.SaveChangesAsync();
 
        var totalRestored = folderIds.Count + pileIds.Count + noteIds.Count;
        TempData["Success"] = $"Restored {totalRestored} {(totalRestored == 1 ? "item" : "items")}.";
        return RedirectToAction("Index", new { tab });
    }
 
    // ══════════════ Delete Permanently ══════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePermanent(
        List<int>? folderIds,
        List<int>? pileIds,
        List<int>? noteIds,
        string tab = "folders")
    {
        var userId = _userManager.GetUserId(User);
 
        folderIds ??= new List<int>();
        pileIds ??= new List<int>();
        noteIds ??= new List<int>();
 
        // ── Standalone notes ──
        if (noteIds.Any())
        {
            var notes = await _context.Notes
                .IgnoreQueryFilters()
                .Where(n => noteIds.Contains(n.Id) && n.UserId == userId)
                .ToListAsync();
            _context.Notes.RemoveRange(notes);
        }
 
        // ── Standalone piles ──
        if (pileIds.Any())
        {
            var piles = await _context.Piles
                .IgnoreQueryFilters()
                .Include(p => p.Folder)
                .Where(p => pileIds.Contains(p.Id) && p.Folder.UserId == userId)
                .ToListAsync();
            _context.Piles.RemoveRange(piles);
        }
 
        // ── Folders + their cascade contents ──
        if (folderIds.Any())
        {
            var folders = await _context.Folders
                .IgnoreQueryFilters()
                .Include(f => f.Piles)
                .Include(f => f.Notes)
                .Where(f => folderIds.Contains(f.Id) && f.UserId == userId)
                .ToListAsync();
 
            foreach (var folder in folders)
            {
                // Notes first (cascades to NoteTag, PileNote, NoteVersion)
                _context.Notes.RemoveRange(folder.Notes);
                // Piles second (cascades to PileNote)
                _context.Piles.RemoveRange(folder.Piles);
            }
 
            await _context.SaveChangesAsync();
 
            // Now delete folders (after piles are gone — Restrict requires this order)
            _context.Folders.RemoveRange(folders);
        }
 
        await _context.SaveChangesAsync();
 
        var totalDeleted = folderIds.Count + pileIds.Count + noteIds.Count;
        TempData["Success"] = $"Permanently deleted {totalDeleted} {(totalDeleted == 1 ? "item" : "items")}.";
        return RedirectToAction("Index", new { tab });
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public DashboardController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
 
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var user = await _userManager.GetUserAsync(User);
 
        var notesQuery = _context.Notes.Where(n => n.UserId == userId);
 
        var hasAnyNotes = await notesQuery.AnyAsync();
 
        var model = new DashboardViewModel
        {
            DisplayName = user?.DisplayName ?? "there",
            IsEmpty = !hasAnyNotes
        };
 
        if (!hasAnyNotes) return View(model);
 
        // ── Recently Opened (3) ──
        model.RecentlyOpened = await notesQuery
            .Where(n => n.LastAccessedAt != null)
            .OrderByDescending(n => n.LastAccessedAt)
            .Take(3)
            .Include(n => n.Folder)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Select(n => MapToDto(n))
            .ToListAsync();
 
        // ── Frequently Visited (3) ──
        model.FrequentlyVisited = await notesQuery
            .Where(n => n.ViewCount > 0)
            .OrderByDescending(n => n.ViewCount)
            .Take(3)
            .Include(n => n.Folder)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Select(n => MapToDto(n))
            .ToListAsync();
 
        // ── Recently Created (3) ──
        model.RecentlyCreated = await notesQuery
            .OrderByDescending(n => n.CreatedAt)
            .Take(3)
            .Include(n => n.Folder)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Select(n => MapToDto(n))
            .ToListAsync();
 
        // ── Random ──
        model.Random = await GetRandomNote(userId!);
 
        return View(model);
    }
 
    // ══════════════ Shuffle Random (AJAX) ════════════════
    [HttpGet]
    public async Task<IActionResult> ShuffleRandom()
    {
        var userId = _userManager.GetUserId(User);
        var note = await GetRandomNote(userId!);
        if (note == null) return Json(new { success = false });
 
        return Json(new
        {
            success = true,
            note = new
            {
                id = note.Id,
                title = note.Title,
                contentPreview = note.ContentPreview,
                folderName = note.FolderName,
                tags = note.Tags.Select(t => new { id = t.Id, name = t.Name, color = t.Color })
            }
        });
    }
 
    private async Task<DashboardNoteDto?> GetRandomNote(string userId)
    {
        var totalCount = await _context.Notes.CountAsync(n => n.UserId == userId);
        if (totalCount == 0) return null;
 
        var skip = new Random().Next(totalCount);
 
        var note = await _context.Notes
            .Where(n => n.UserId == userId)
            .Include(n => n.Folder)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .OrderBy(n => n.Id)
            .Skip(skip)
            .Take(1)
            .FirstOrDefaultAsync();
 
        return note == null ? null : MapToDto(note);
    }
 
    private static DashboardNoteDto MapToDto(Note n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        ContentPreview = System.Text.RegularExpressions.Regex.Replace(n.Content ?? "", "<.*?>", " ").Trim(),
        FolderName = n.Folder?.Name,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt,
        LastAccessedAt = n.LastAccessedAt,
        ViewCount = n.ViewCount,
        Tags = n.NoteTags.Select(nt => new TagListViewModel
        {
            Id = nt.Tag.Id,
            Name = nt.Tag.Name,
            Color = nt.Tag.Color
        }).OrderBy(t => t.Name).ToList()
    };
}
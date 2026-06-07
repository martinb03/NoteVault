using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class NotesController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public NotesController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
 
    // ══════════════ Notes List ══════════════════════════
    [HttpGet]
    public async Task<IActionResult> Index(bool unfiledOnly = false)
    {
        var userId = _userManager.GetUserId(User);
 
        var query = _context.Notes.Where(n => n.UserId == userId);
        if (unfiledOnly) query = query.Where(n => n.FolderId == null);
 
        var notes = await query
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteListViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync();
 
        var model = new NoteListPageViewModel
        {
            Notes = notes,
            ShowUnfiledOnly = unfiledOnly,
            AvailableFolders = await GetUserFolders(userId!)
        };
 
        return View(model);
    }
 
    // ══════════════ Create Note (from modal) ═══════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateNoteViewModel model, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Title is required.";
            return RedirectToAction("Index");
        }
 
        var note = new Note
        {
            Title = model.Title,
            Content = string.Empty,
            FolderId = model.FolderId,
            UserId = userId!,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
 
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl))
        {
            TempData["Success"] = $"Note \"{note.Title}\" created.";
            return Redirect(returnUrl);
        }
 
        return RedirectToAction("Edit", new { id = note.Id });
    }
 
    // ══════════════ Note Details ════════════════════════
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .Include(n => n.Folder)
            .Include(n => n.Versions)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        note.ViewCount++;
        note.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
 
        var model = new NoteDetailsViewModel
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            FolderId = note.FolderId,
            FolderName = note.Folder?.Name,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
            VersionCount = note.Versions.Count,
            AvailableFolders = await GetUserFolders(userId!)
        };
 
        return View(model);
    }
 
    // ══════════════ Edit Note (Quill editor) ════════════
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .Include(n => n.Folder)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        var model = new EditNoteViewModel
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            FolderId = note.FolderId,
            FolderName = note.Folder?.Name,
            UpdatedAt = note.UpdatedAt
        };
 
        return View(model);
    }
 
    // ══════════════ Auto-save (AJAX) ════════════════════
    [HttpPost]
    public async Task<IActionResult> AutoSave([FromBody] AutoSaveRequest request)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        note.Title = request.Title;
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        return Json(new { success = true, updatedAt = note.UpdatedAt.ToString("o") });
    }
 
    // ══════════════ Change Parent Folder ════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeFolder(int id, int? folderId)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .Include(n => n.PileNotes)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        if (note.FolderId != folderId)
        {
            _context.PileNotes.RemoveRange(note.PileNotes);
        }
 
        note.FolderId = folderId;
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Folder updated.";
        return RedirectToAction("Details", new { id });
    }
 
    // ══════════════ Save Version ════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVersion(int id, string? name)
    {
        Console.WriteLine($"DEBUG: name received = '{name}'");
        
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .Include(n => n.Versions)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        var nextVersion = note.Versions.Any()
            ? note.Versions.Max(v => v.VersionNumber) + 1
            : 1;
 
        var version = new NoteVersion
        {
            NoteId = note.Id,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Content = note.Content,
            VersionNumber = nextVersion,
            CreatedAt = DateTime.UtcNow
        };
 
        _context.NoteVersions.Add(version);
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Version {nextVersion} saved.";
        return RedirectToAction("Edit", new { id });
    }
 
    // ══════════════ Soft Delete Note ════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        note.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Note \"{note.Title}\" moved to trash.";
 
        return !string.IsNullOrEmpty(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index");
    }
 
    // ══════════════ Versions List ═══════════════════════
    [HttpGet]
    public async Task<IActionResult> Versions(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .Include(n => n.Folder)
            .Include(n => n.Versions)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        var model = new VersionListPageViewModel
        {
            NoteId = note.Id,
            NoteTitle = note.Title,
            FolderId = note.FolderId,
            FolderName = note.Folder?.Name,
            Versions = note.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new VersionListItemViewModel
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    Name = v.Name,
                    CreatedAt = v.CreatedAt,
                })
                .ToList()
        };
 
        return View(model);
    }
 
    // ══════════════ Version Details ═════════════════════
    [HttpGet]
    public async Task<IActionResult> VersionDetails(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .ThenInclude(n => n.Folder)
            .FirstOrDefaultAsync(v => v.Id == id && v.Note.UserId == userId);
 
        if (version == null) return NotFound();
 
        var model = new VersionDetailsViewModel
        {
            Id = version.Id,
            NoteId = version.NoteId,
            NoteTitle = version.Note.Title,
            VersionNumber = version.VersionNumber,
            Name = version.Name,
            Content = version.Content,
            CreatedAt = version.CreatedAt,
            FolderId = version.Note.FolderId,
            FolderName = version.Note.Folder?.Name
        };
 
        return View(model);
    }
 
    // ══════════════ Update Version Name ════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVersionName(int id, string? name)
    {
        var userId = _userManager.GetUserId(User);
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .FirstOrDefaultAsync(v => v.Id == id && v.Note.UserId == userId);
 
        if (version == null) return NotFound();
 
        version.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Version name updated.";
        return RedirectToAction("VersionDetails", new { id });
    }
 
    // ══════════════ Delete Version (permanent) ══════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersion(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .FirstOrDefaultAsync(v => v.Id == id && v.Note.UserId == userId);
 
        if (version == null) return NotFound();
 
        var noteId = version.NoteId;
        _context.NoteVersions.Remove(version);
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Version deleted.";
        return RedirectToAction("Versions", new { id = noteId });
    }
 
    // ══════════════ Restore Version ═════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreVersion(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .FirstOrDefaultAsync(v => v.Id == id && v.Note.UserId == userId);
 
        if (version == null) return NotFound();
 
        version.Note.Content = version.Content;
        version.Note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Restored to version {version.VersionNumber}.";
        return RedirectToAction("Edit", new { id = version.NoteId });
    }
 
    // ── Helper ──────────────────────────────────────────
    private async Task<List<FolderSelectItem>> GetUserFolders(string userId)
    {
        return await _context.Folders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new FolderSelectItem { Id = f.Id, Name = f.Name })
            .ToListAsync();
    }
}
 
public class AutoSaveRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
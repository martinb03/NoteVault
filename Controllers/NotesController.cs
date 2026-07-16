using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.Services;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class NotesController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RazorViewRenderer _viewRenderer;
    private readonly PdfService _pdfService;
    private readonly IPermissionService _permissionService;
    
    private static readonly TimeSpan LockInactivityTimeout = TimeSpan.FromMinutes(5);
 
    public NotesController(AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RazorViewRenderer viewRenderer,
        PdfService pdfService,
        IPermissionService permissionService)
    {
        _context = context;
        _userManager = userManager;
        _viewRenderer = viewRenderer;
        _pdfService = pdfService;
        _permissionService = permissionService;
    }
 
    // ======= Notes List =======
    [HttpGet]
    public async Task<IActionResult> Index(bool unfiledOnly = false)
    {
        var userId = _userManager.GetUserId(User);
 
        var query = _context.Notes.Where(n => n.UserId == userId);
        if (unfiledOnly) query = query.Where(n => n.FolderId == null);
 
        var notes = await query
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
                    .Select(nt => new TagListViewModel
                    {
                        Id = nt.Tag.Id,
                        Name = nt.Tag.Name,
                        Color = nt.Tag.Color
                    })
                    .OrderBy(t=>t.Name)
                    .ToList()
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
 
    // ------- Create Note (from modal) -------
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
 
    // ------- Note Details -------
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var note = await _context.Notes
            .Include(n => n.Folder)
            .Include(n => n.Versions)
            .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
 
        if (note == null) return NotFound();
 
        note.ViewCount++;
        note.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var noteTagIds = note.NoteTags.Select(nt => nt.TagId).ToHashSet();
        
        var allUserTags = await _context.Tags
            .Where(t=>t.UserId == userId)
            .OrderBy(t=>t.Name)
            .Select(t=>new TagListViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .ToListAsync();
        
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
            AvailableFolders = await GetUserFolders(userId!),
            Tags = note.NoteTags
                .Select(nt=>new TagListViewModel
                {
                    Id = nt.Tag.Id,
                    Name = nt.Tag.Name,
                    Color = nt.Tag.Color
                })
                .OrderBy(t=>t.Name)
                .ToList(),
            AvailableTags = allUserTags
                .Where(t=>!noteTagIds.Contains(t.Id))
                .ToList()
        };
 
        return View(model);
    }
 
    // ------- Edit Note (Quill editor) -------
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
 
    // ------- Auto-save (AJAX) -------
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
 
    // ------- Change Parent Folder -------
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
 
    // ------- Add Tags to Note -------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTags(int id, List<int> tagIds)
    {
        var userId = _userManager.GetUserId(User);

        var note = await _context.Notes
            .Include(n => n.NoteTags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null) return NotFound();

        // Only add tags that belong to this user and aren't already on the note
        var validTagIds = await _context.Tags
            .Where(t => tagIds.Contains(t.Id) && t.UserId == userId)
            .Select(t => t.Id)
            .ToListAsync();

        var currentTagIds = note.NoteTags.Select(nt => nt.TagId).ToHashSet();

        foreach (var tagId in validTagIds.Where(tid => !currentTagIds.Contains(tid)))
        {
            _context.NoteTags.Add(new NoteTag { NoteId = note.Id, TagId = tagId });
        }

        await _context.SaveChangesAsync();

        return RedirectToAction("Details", new { id, from = Request.Query["from"].ToString() });
    }

// ------- Remove Tag from Note -------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTag(int id, int tagId)
    {
        var userId = _userManager.GetUserId(User);

        var note = await _context.Notes
            .Include(n => n.NoteTags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null) return NotFound();

        var noteTag = note.NoteTags.FirstOrDefault(nt => nt.TagId == tagId);
        if (noteTag != null)
        {
            _context.NoteTags.Remove(noteTag);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Details", new { id, from = Request.Query["from"].ToString() });
    }
    
    // ------- Save Version -------
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
 
    // ------- Soft Delete Note -------
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
 
    // ------- Versions List -------
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
 
    // ------- Version Details -------
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
 
    // ------- Update Version Name -------
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
 
    // ------- Delete Version (permanent) -------
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
 
    // ------- Restore Version -------
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
 
    // ------- Helper -------
    private async Task<List<FolderSelectItem>> GetUserFolders(string userId)
    {
        return await _context.Folders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new FolderSelectItem { Id = f.Id, Name = f.Name })
            .ToListAsync();
    }
    
    [HttpGet]
    public async Task<IActionResult> Export(int id)
    {
        var userId = _userManager.GetUserId(User);

        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null) return NotFound();

        var html = await _viewRenderer.RenderAsync("/Views/Shared/Export/_NoteExport.cshtml",
            new NoteExportModel { Title = note.Title, Content = note.Content });

        var pdfBytes = await _pdfService.HtmlToPdfAsync(html);
        var filename = $"{FileNameSanitizer.Sanitize(note.Title)}.pdf";

        return File(pdfBytes, "application/pdf", filename);
    }
    
    /* Summary:
    Acquire an edit lock on a note. Called when the edit view opens.
    Behavior:
     - Cleans up stale locks (older than the inactivity timeout) before acting.
     - If the caller already holds the lock, refresh it.
     - If someone else holds an active lock, return conflict info (frontend shows the shaded view).
     - Otherwise creates a new lock.*/
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcquireLock(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        await CleanupStaleLockAsync(id);
 
        var lockRow = await _context.NoteEditLocks
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.NoteId == id);
 
        if (lockRow == null)
        {
            _context.NoteEditLocks.Add(new NoteEditLock
            {
                NoteId = id,
                UserId = userId,
                AcquiredAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Json(new { success = true, acquired = true });
        }
 
        if (lockRow.UserId == userId)
        {
            lockRow.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Json(new { success = true, acquired = true });
        }
 
        // Someone else holds it
        return Json(new
        {
            success = false,
            acquired = false,
            heldByUserId = lockRow.UserId,
            heldByName = lockRow.User.DisplayName
        });
    }
    
    /* Summary:
    Refresh the current lock. Called by the client every 60 seconds while editing.
    Fails if the caller no longer holds the lock (someone took control).*/

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshLock(int id)
    {
        var userId = _userManager.GetUserId(User)!;
     
        var lockRow = await _context.NoteEditLocks
            .FirstOrDefaultAsync(l => l.NoteId == id);
     
        if (lockRow == null || lockRow.UserId != userId)
            return Json(new { success = false });
     
        lockRow.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
     
    // Release the caller's lock. Idempotent: no-op if the caller doesn't hold it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseLock(int id)
    {
        var userId = _userManager.GetUserId(User)!;

        var lockRow = await _context.NoteEditLocks
            .FirstOrDefaultAsync(l => l.NoteId == id && l.UserId == userId);

        if (lockRow != null)
        {
            _context.NoteEditLocks.Remove(lockRow);
            await _context.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    /* Summary:
    Owner-only: forcibly take control from whoever currently holds the lock.
    Called when the owner clicks "Take control" on the Details view.*/
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TakeControl(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission != EffectivePermission.Owner)
            return Forbid();
     
        var lockRow = await _context.NoteEditLocks
            .FirstOrDefaultAsync(l => l.NoteId == id);
     
        if (lockRow != null)
        {
            _context.NoteEditLocks.Remove(lockRow);
            await _context.SaveChangesAsync();
        }
     
        _context.NoteEditLocks.Add(new NoteEditLock
        {
            NoteId = id,
            UserId = userId,
            AcquiredAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
     
        return Json(new { success = true });
    }
     
    /// <summary>
    /// Remove any lock whose LastActivityAt is older than the inactivity timeout.
    /// Called at the start of AcquireLock so idle sessions don't hold notes hostage.
    /// </summary>
    private async Task CleanupStaleLockAsync(int noteId)
    {
        var cutoff = DateTime.UtcNow - LockInactivityTimeout;
        var stale = await _context.NoteEditLocks
            .Where(l => l.NoteId == noteId && l.LastActivityAt < cutoff)
            .ToListAsync();
     
        if (stale.Count > 0)
        {
            _context.NoteEditLocks.RemoveRange(stale);
            await _context.SaveChangesAsync();
        }
    }
}
 
public class AutoSaveRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
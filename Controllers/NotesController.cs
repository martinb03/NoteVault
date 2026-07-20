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
        var userId = _userManager.GetUserId(User)!;
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Title is required.";
            return RedirectToAction("Index");
        }
 
        // Default: caller owns the new note
        string ownerUserId = userId;
 
        // If creating inside a folder, verify permission and honor the "folder owner
        // owns everything inside" rule for shared folders.
        if (model.FolderId != null)
        {
            var folderPermission = await _permissionService.GetFolderPermissionAsync(userId, model.FolderId.Value);
            if (folderPermission < EffectivePermission.Edit)
                return Forbid();
 
            if (folderPermission != EffectivePermission.Owner)
            {
                var folder = await _context.Folders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == model.FolderId.Value);
                if (folder == null) return NotFound();
                ownerUserId = folder.UserId;
            }
        }
 
        var note = new Note
        {
            Title = model.Title,
            Content = string.Empty,
            FolderId = model.FolderId,
            UserId = ownerUserId,
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
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission == EffectivePermission.None)
            return Forbid();
     
        var note = await _context.Notes
            .Include(n => n.Folder)
            .Include(n => n.Versions)
            .FirstOrDefaultAsync(n => n.Id == id);
     
        if (note == null) return NotFound();
     
        var isOwner = permission == EffectivePermission.Owner;
     
        // Only track access stats for the owner viewing their own note,
        // otherwise "Frequently visited" would be skewed by recipients.
        if (isOwner)
        {
            note.ViewCount++;
            note.LastAccessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
     
        // Owner info (needed for shared notes to show "Owned by X")
        var owner = await _userManager.FindByIdAsync(note.UserId);
        var ownerName = owner?.DisplayName ?? "";
     
        // Current user's private tags on this note
        var currentUserTagsOnNote = await _context.NoteTags
            .Include(nt => nt.Tag)
            .Where(nt => nt.NoteId == id && nt.Tag.UserId == userId)
            .Select(nt => new TagListViewModel
            {
                Id = nt.Tag.Id,
                Name = nt.Tag.Name,
                Color = nt.Tag.Color
            })
            .OrderBy(t => t.Name)
            .ToListAsync();
     
        var noteTagIds = currentUserTagsOnNote.Select(t => t.Id).ToHashSet();
     
        var allUserTags = await _context.Tags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TagListViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .ToListAsync();
     
        // Copy chip metadata — resolve the source user's current display name
        string? copiedFromUserName = null;
        if (note.CopiedFromUserId != null)
        {
            var copySourceUser = await _userManager.FindByIdAsync(note.CopiedFromUserId);
            copiedFromUserName = copySourceUser?.DisplayName;
        }
     
        // Lock state
        var lockRow = await _context.NoteEditLocks
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.NoteId == id);
        string? lockHeldByUserId = null;
        string? lockHeldByName = null;
        if (lockRow != null)
        {
            var cutoff = DateTime.UtcNow - LockInactivityTimeout;
            if (lockRow.LastActivityAt >= cutoff)
            {
                lockHeldByUserId = lockRow.UserId;
                lockHeldByName = lockRow.User.DisplayName;
            }
        }
     
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
            AvailableFolders = isOwner ? await GetUserFolders(userId) : new(),  // recipients can't move
            Tags = currentUserTagsOnNote,
            AvailableTags = allUserTags.Where(t => !noteTagIds.Contains(t.Id)).ToList(),
     
            CurrentUserPermission = permission.ToString(),
            IsOwner = isOwner,
            OwnerUserId = note.UserId,
            OwnerName = ownerName,
            LockHeldByUserId = lockHeldByUserId,
            LockHeldByName = lockHeldByName,
            CopiedFromUserId = note.CopiedFromUserId,
            CopiedFromUserName = copiedFromUserName,
            CopiedFromTitle = note.CopiedFromTitle
        };
     
        return View(model);
    }
 
    // ------- Edit Note (Quill editor) -------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        var note = await _context.Notes
            .Include(n => n.Folder)
            .FirstOrDefaultAsync(n => n.Id == id);
 
        if (note == null) return NotFound();
 
        // Lock state — view uses this to render either the editor or the shaded read-only overlay
        var lockRow = await _context.NoteEditLocks
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.NoteId == id);
        string? lockHeldByUserId = null;
        string? lockHeldByName = null;
        if (lockRow != null)
        {
            var cutoff = DateTime.UtcNow - LockInactivityTimeout;
            if (lockRow.LastActivityAt >= cutoff && lockRow.UserId != userId)
            {
                lockHeldByUserId = lockRow.UserId;
                lockHeldByName = lockRow.User.DisplayName;
            }
        }
 
        var model = new EditNoteViewModel
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            FolderId = note.FolderId,
            FolderName = note.Folder?.Name,
            UpdatedAt = note.UpdatedAt,
            CurrentUserPermission = permission.ToString(),
            IsOwner = permission == EffectivePermission.Owner,
            LockHeldByUserId = lockHeldByUserId,
            LockHeldByName = lockHeldByName
        };
 
        return View(model);
    }
 
    // ------- Auto-save (AJAX) -------
    [HttpPost]
    public async Task<IActionResult> AutoSave([FromBody] AutoSaveRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, request.Id);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        // Verify the caller still holds the lock; if not, refuse the save.
        var lockRow = await _context.NoteEditLocks
            .FirstOrDefaultAsync(l => l.NoteId == request.Id);
        if (lockRow == null || lockRow.UserId != userId)
            return Json(new { success = false, reason = "lock_lost" });
 
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == request.Id);
        if (note == null) return NotFound();
 
        note.Title = request.Title;
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;
 
        // Refresh the lock — activity resets the inactivity clock
        lockRow.LastActivityAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        return Json(new { success = true, updatedAt = note.UpdatedAt.ToString("o") });
    }
 
    // ------- Change Parent Folder -------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeFolder(int id, int? folderId)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission != EffectivePermission.Owner)
            return Forbid();
 
        var note = await _context.Notes
            .Include(n => n.PileNotes)
            .FirstOrDefaultAsync(n => n.Id == id);
 
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
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission == EffectivePermission.None)
            return Forbid();
 
        var noteExists = await _context.Notes.AnyAsync(n => n.Id == id);
        if (!noteExists) return NotFound();
 
        // Only add tags belonging to the current user
        var validTagIds = await _context.Tags
            .Where(t => tagIds.Contains(t.Id) && t.UserId == userId)
            .Select(t => t.Id)
            .ToListAsync();
 
        // Filter out tags this user has already applied to this note
        var currentUserTagsOnNote = (await _context.NoteTags
                .Include(nt => nt.Tag)
                .Where(nt => nt.NoteId == id && nt.Tag.UserId == userId)
                .Select(nt => nt.TagId)
                .ToListAsync())
            .ToHashSet();
 
        foreach (var tagId in validTagIds.Where(tid => !currentUserTagsOnNote.Contains(tid)))
        {
            _context.NoteTags.Add(new NoteTag { NoteId = id, TagId = tagId });
        }
 
        await _context.SaveChangesAsync();
 
        return RedirectToAction("Details", new { id, from = Request.Query["from"].ToString() });
    }

    // ------- Remove Tag from Note -------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTag(int id, int tagId)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission == EffectivePermission.None)
            return Forbid();
 
        // Only remove a tag if it belongs to the current user
        var noteTag = await _context.NoteTags
            .Include(nt => nt.Tag)
            .FirstOrDefaultAsync(nt => nt.NoteId == id && nt.TagId == tagId && nt.Tag.UserId == userId);
 
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
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        // Caller must hold the lock
        var lockRow = await _context.NoteEditLocks.FirstOrDefaultAsync(l => l.NoteId == id);
        if (lockRow == null || lockRow.UserId != userId)
        {
            TempData["Error"] = "You no longer hold the editing lock for this note.";
            return RedirectToAction("Details", new { id });
        }
 
        var note = await _context.Notes
            .Include(n => n.Versions)
            .FirstOrDefaultAsync(n => n.Id == id);
 
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
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission != EffectivePermission.Owner)
            return Forbid();
 
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
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
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission == EffectivePermission.None)
            return Forbid();
 
        var note = await _context.Notes
            .Include(n => n.Folder)
            .Include(n => n.Versions)
            .FirstOrDefaultAsync(n => n.Id == id);
 
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
        var userId = _userManager.GetUserId(User)!;
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .ThenInclude(n => n.Folder)
            .FirstOrDefaultAsync(v => v.Id == id);
 
        if (version == null) return NotFound();
 
        var permission = await _permissionService.GetNotePermissionAsync(userId, version.NoteId);
        if (permission == EffectivePermission.None)
            return Forbid();
 
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
        var userId = _userManager.GetUserId(User)!;
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .FirstOrDefaultAsync(v => v.Id == id);
 
        if (version == null) return NotFound();
 
        var permission = await _permissionService.GetNotePermissionAsync(userId, version.NoteId);
        if (permission != EffectivePermission.Owner)
            return Forbid();
 
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
        var userId = _userManager.GetUserId(User)!;
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .FirstOrDefaultAsync(v => v.Id == id);
 
        if (version == null) return NotFound();
 
        var permission = await _permissionService.GetNotePermissionAsync(userId, version.NoteId);
        if (permission != EffectivePermission.Owner)
            return Forbid();
 
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
        var userId = _userManager.GetUserId(User)!;
 
        var version = await _context.NoteVersions
            .Include(v => v.Note)
            .FirstOrDefaultAsync(v => v.Id == id);
 
        if (version == null) return NotFound();
 
        var permission = await _permissionService.GetNotePermissionAsync(userId, version.NoteId);
        if (permission != EffectivePermission.Owner)
            return Forbid();
 
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
    
    // ------- Export -------
    [HttpGet]
    public async Task<IActionResult> Export(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, id);
        if (permission == EffectivePermission.None)
            return Forbid();
 
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
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
     
    /* Summary
    Remove any lock whose LastActivityAt is older than the inactivity timeout.
    Called at the start of AcquireLock so idle sessions don't hold notes hostage.*/
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
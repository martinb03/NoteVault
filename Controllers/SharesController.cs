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
public class SharesController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;

    public SharesController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IPermissionService permissionService)
    {
        _context = context;
        _userManager = userManager;
        _permissionService = permissionService;
    }

    // ======= Share modal: GET current state =======

    [HttpGet]
    public async Task<IActionResult> GetNoteShares(int noteId)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, noteId);

        if (permission < EffectivePermission.EditAndShare)
            return Forbid();

        var note = await _context.Notes
            .AsNoTracking()
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note == null)
            return NotFound();

        var shares = await _context.NoteShares
            .AsNoTracking()
            .Include(ns => ns.SharedWithUser)
            .Include(ns => ns.SharedByUser)
            .Where(ns => ns.NoteId == noteId)
            .ToListAsync();

        var model = BuildShareModal(
            ownerName: note.User.DisplayName,
            isOwner: permission == EffectivePermission.Owner,
            permission: permission,
            currentUserId: userId,
            shares: shares.Select(ns => (
                ns.SharedWithUserId,
                ns.SharedWithUser.DisplayName,
                ns.SharedWithUser.Email ?? "",
                ns.Permission,
                ns.SharedByUserId,
                ns.SharedByUser.DisplayName
            )).ToList());

        return Json(model);
    }

    [HttpGet]
    public async Task<IActionResult> GetFolderShares(int folderId)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetFolderPermissionAsync(userId, folderId);

        if (permission < EffectivePermission.EditAndShare)
            return Forbid();

        var folder = await _context.Folders
            .AsNoTracking()
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null)
            return NotFound();

        var shares = await _context.FolderShares
            .AsNoTracking()
            .Include(fs => fs.SharedWithUser)
            .Include(fs => fs.SharedByUser)
            .Where(fs => fs.FolderId == folderId)
            .ToListAsync();

        var model = BuildShareModal(
            ownerName: folder.User.DisplayName,
            isOwner: permission == EffectivePermission.Owner,
            permission: permission,
            currentUserId: userId,
            shares: shares.Select(fs => (
                fs.SharedWithUserId,
                fs.SharedWithUser.DisplayName,
                fs.SharedWithUser.Email ?? "",
                fs.Permission,
                fs.SharedByUserId,
                fs.SharedByUser.DisplayName
            )).ToList());

        return Json(model);
    }

    private static ShareModalViewModel BuildShareModal(
        string ownerName,
        bool isOwner,
        EffectivePermission permission,
        string currentUserId,
        List<(string SharedWithUserId, string DisplayName, string Email,
              SharePermission Permission, string SharedByUserId, string SharedByName)> shares)
    {
        return new ShareModalViewModel
        {
            OwnerName = ownerName,
            IsCurrentUserOwner = isOwner,
            CurrentUserPermission = permission.ToString(),
            Shares = shares.Select(s => new ShareEntryViewModel
            {
                UserId = s.SharedWithUserId,
                DisplayName = s.DisplayName,
                Email = s.Email,
                Permission = s.Permission.ToString(),
                SharedByUserId = s.SharedByUserId,
                SharedByName = s.SharedByName,
                CanManage = isOwner || s.SharedByUserId == currentUserId,
                DownstreamCount = shares.Count(x => x.SharedByUserId == s.SharedWithUserId)
            }).ToList()
        };
    }

    // ======= Share modal: atomic update =======

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNoteShares([FromBody] UpdateSharesRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, request.ResourceId);

        if (permission < EffectivePermission.EditAndShare)
            return Forbid();

        var note = await _context.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.ResourceId);
        if (note == null)
            return NotFound();

        var isOwner = permission == EffectivePermission.Owner;

        var existingShares = await _context.NoteShares
            .Where(ns => ns.NoteId == request.ResourceId)
            .ToListAsync();

        var validationError = ValidateShareChanges(
            request, isOwner, userId, note.UserId,
            existingShares.Select(s => (s.SharedWithUserId, s.SharedByUserId)).ToList());
        if (validationError != null)
            return BadRequest(new { error = validationError });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Adds (upsert: if already shared, becomes an update)
            foreach (var add in request.Adds)
            {
                var parsed = ParsePermission(add.Permission);
                if (parsed == null) return BadRequest(new { error = $"Invalid permission '{add.Permission}'." });

                var existing = existingShares.FirstOrDefault(s => s.SharedWithUserId == add.UserId);
                if (existing != null)
                {
                    existing.Permission = parsed.Value;
                }
                else
                {
                    _context.NoteShares.Add(new NoteShare
                    {
                        NoteId = request.ResourceId,
                        SharedWithUserId = add.UserId,
                        SharedByUserId = userId,
                        Permission = parsed.Value,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // Updates
            foreach (var update in request.Updates)
            {
                var parsed = ParsePermission(update.Permission);
                if (parsed == null) return BadRequest(new { error = $"Invalid permission '{update.Permission}'." });

                var existing = existingShares.FirstOrDefault(s => s.SharedWithUserId == update.UserId);
                if (existing != null)
                    existing.Permission = parsed.Value;
            }

            // Removals (with optional downstream cascade)
            foreach (var removal in request.Removals)
            {
                var existing = existingShares.FirstOrDefault(s => s.SharedWithUserId == removal.UserId);
                if (existing != null)
                    _context.NoteShares.Remove(existing);

                if (removal.CascadeDownstream)
                {
                    var downstream = existingShares
                        .Where(s => s.SharedByUserId == removal.UserId)
                        .ToList();
                    _context.NoteShares.RemoveRange(downstream);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFolderShares([FromBody] UpdateSharesRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetFolderPermissionAsync(userId, request.ResourceId);

        if (permission < EffectivePermission.EditAndShare)
            return Forbid();

        var folder = await _context.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.ResourceId);
        if (folder == null)
            return NotFound();

        var isOwner = permission == EffectivePermission.Owner;

        var existingShares = await _context.FolderShares
            .Where(fs => fs.FolderId == request.ResourceId)
            .ToListAsync();

        var validationError = ValidateShareChanges(
            request, isOwner, userId, folder.UserId,
            existingShares.Select(s => (s.SharedWithUserId, s.SharedByUserId)).ToList());
        if (validationError != null)
            return BadRequest(new { error = validationError });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var add in request.Adds)
            {
                var parsed = ParsePermission(add.Permission);
                if (parsed == null) return BadRequest(new { error = $"Invalid permission '{add.Permission}'." });

                var existing = existingShares.FirstOrDefault(s => s.SharedWithUserId == add.UserId);
                if (existing != null)
                {
                    existing.Permission = parsed.Value;
                }
                else
                {
                    _context.FolderShares.Add(new FolderShare
                    {
                        FolderId = request.ResourceId,
                        SharedWithUserId = add.UserId,
                        SharedByUserId = userId,
                        Permission = parsed.Value,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            foreach (var update in request.Updates)
            {
                var parsed = ParsePermission(update.Permission);
                if (parsed == null) return BadRequest(new { error = $"Invalid permission '{update.Permission}'." });

                var existing = existingShares.FirstOrDefault(s => s.SharedWithUserId == update.UserId);
                if (existing != null)
                    existing.Permission = parsed.Value;
            }

            foreach (var removal in request.Removals)
            {
                var existing = existingShares.FirstOrDefault(s => s.SharedWithUserId == removal.UserId);
                if (existing != null)
                    _context.FolderShares.Remove(existing);

                if (removal.CascadeDownstream)
                {
                    var downstream = existingShares
                        .Where(s => s.SharedByUserId == removal.UserId)
                        .ToList();
                    _context.FolderShares.RemoveRange(downstream);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return Json(new { success = true });
    }

    // ======= Shared validation =======

    /* Summary:
    Validates a share-change request against the permission rules.
    Returns an error message, or null if valid*/
    private static string? ValidateShareChanges(
        UpdateSharesRequest request,
        bool isOwner,
        string currentUserId,
        string ownerUserId,
        List<(string SharedWithUserId, string SharedByUserId)> existingShares)
    {
        var allTargets = request.Adds.Select(a => a.UserId)
            .Concat(request.Updates.Select(u => u.UserId))
            .Concat(request.Removals.Select(r => r.UserId));

        foreach (var target in allTargets)
        {
            if (target == ownerUserId)
                return "The owner's access cannot be modified.";
            if (target == currentUserId)
                return "You cannot modify your own access.";
        }

        if (!isOwner)
        {
            // Permission ceiling: Edit+Share callers can only grant View or Edit.
            // This rule is also what makes single-level downstream cascade complete:
            // no one downstream of the owner can create further sharers.
            foreach (var p in request.Adds.Select(a => a.Permission)
                         .Concat(request.Updates.Select(u => u.Permission)))
            {
                if (p == nameof(SharePermission.EditAndShare))
                    return "Only the owner can grant Edit + Share permission.";
            }

            // Management scope: non-owners only touch shares they initiated
            foreach (var update in request.Updates)
            {
                var share = existingShares.FirstOrDefault(s => s.SharedWithUserId == update.UserId);
                if (share != default && share.SharedByUserId != currentUserId)
                    return "You can only modify shares you created.";
            }

            foreach (var removal in request.Removals)
            {
                var share = existingShares.FirstOrDefault(s => s.SharedWithUserId == removal.UserId);
                if (share != default && share.SharedByUserId != currentUserId)
                    return "You can only remove shares you created.";
            }
        }

        return null;
    }
    
        // ======= Shared with me: view =======
     
    [HttpGet]
    public async Task<IActionResult> SharedWithMe()
    {
        var userId = _userManager.GetUserId(User)!;
     
        // 1. Notes shared directly with the user (not via a folder)
        var directNoteShares = await _context.NoteShares
            .AsNoTracking()
            .Include(ns => ns.Note).ThenInclude(n => n.User)
            .Include(ns => ns.SharedByUser)
            .Where(ns => ns.SharedWithUserId == userId)
            .Select(ns => new SharedItemViewModel
            {
                ItemType = "Note",
                Id = ns.NoteId,
                Title = ns.Note.Title,
                OwnerName = ns.Note.User.DisplayName,
                OwnerUserId = ns.Note.UserId,
                SharedByName = ns.SharedByUser.DisplayName,
                SharedByUserId = ns.SharedByUserId,
                Permission = ns.Permission.ToString(),
                UpdatedAt = ns.Note.UpdatedAt,
                SharedAt = ns.CreatedAt
            })
            .ToListAsync();
     
        // 2. Notes accessible via a shared folder (no explicit direct share).
        //    A note directly shared AND in a shared folder appears only once (from step 1) —
        //    we exclude such notes here to avoid duplicates.
        var directNoteIds = directNoteShares.Select(n => n.Id).ToHashSet();
     
        var folderNoteAccess = await _context.FolderShares
            .AsNoTracking()
            .Include(fs => fs.Folder).ThenInclude(f => f.User)
            .Include(fs => fs.SharedByUser)
            .Where(fs => fs.SharedWithUserId == userId)
            .SelectMany(fs => fs.Folder.Notes
                .Where(n => !directNoteIds.Contains(n.Id))
                .Select(n => new SharedItemViewModel
                {
                    ItemType = "Note",
                    Id = n.Id,
                    Title = n.Title,
                    OwnerName = fs.Folder.User.DisplayName,
                    OwnerUserId = fs.Folder.UserId,
                    SharedByName = fs.SharedByUser.DisplayName,
                    SharedByUserId = fs.SharedByUserId,
                    Permission = fs.Permission.ToString(),
                    UpdatedAt = n.UpdatedAt,
                    SharedAt = fs.CreatedAt
                }))
            .ToListAsync();
     
        // 3. Folders shared with the user
        var folderShares = await _context.FolderShares
            .AsNoTracking()
            .Include(fs => fs.Folder).ThenInclude(f => f.User)
            .Include(fs => fs.SharedByUser)
            .Where(fs => fs.SharedWithUserId == userId)
            .Select(fs => new SharedItemViewModel
            {
                ItemType = "Folder",
                Id = fs.FolderId,
                Title = fs.Folder.Name,
                OwnerName = fs.Folder.User.DisplayName,
                OwnerUserId = fs.Folder.UserId,
                SharedByName = fs.SharedByUser.DisplayName,
                SharedByUserId = fs.SharedByUserId,
                Permission = fs.Permission.ToString(),
                UpdatedAt = fs.Folder.UpdatedAt,
                SharedAt = fs.CreatedAt
            })
            .ToListAsync();
     
        var items = directNoteShares
            .Concat(folderNoteAccess)
            .Concat(folderShares)
            .OrderByDescending(i => i.SharedAt)
            .ToList();
     
        // Attach recipient's private tags to each note item
        var noteIds = items.Where(i => i.ItemType == "Note").Select(i => i.Id).ToList();
        if (noteIds.Count > 0)
        {
            var privateTagsByNote = await _context.NoteTags
                .AsNoTracking()
                .Include(nt => nt.Tag)
                .Where(nt => noteIds.Contains(nt.NoteId) && nt.Tag.UserId == userId)
                .GroupBy(nt => nt.NoteId)
                .Select(g => new
                {
                    NoteId = g.Key,
                    Tags = g.Select(nt => new TagListViewModel
                    {
                        Id = nt.Tag.Id,
                        Name = nt.Tag.Name,
                        Color = nt.Tag.Color
                    }).ToList()
                })
                .ToDictionaryAsync(x => x.NoteId, x => x.Tags);
     
            foreach (var item in items.Where(i => i.ItemType == "Note"))
            {
                if (privateTagsByNote.TryGetValue(item.Id, out var tags))
                    item.PrivateTags = tags;
            }
        }
     
        // Filter dropdown options (deduped)
        var sharedByOptions = items
            .Select(i => new UserFilterOption { UserId = i.SharedByUserId, DisplayName = i.SharedByName })
            .DistinctBy(o => o.UserId)
            .OrderBy(o => o.DisplayName)
            .ToList();
     
        var ownedByOptions = items
            .Select(i => new UserFilterOption { UserId = i.OwnerUserId, DisplayName = i.OwnerName })
            .DistinctBy(o => o.UserId)
            .OrderBy(o => o.DisplayName)
            .ToList();
     
        var model = new SharedWithMePageViewModel
        {
            Items = items,
            SharedByOptions = sharedByOptions,
            OwnedByOptions = ownedByOptions
        };
     
        return View(model);
    }
     
    // ======= Save copy =======
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCopy([FromBody] SaveCopyRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetNotePermissionAsync(userId, request.NoteId);
        if (permission == EffectivePermission.None)
            return Forbid();
     
        var source = await _context.Notes
            .AsNoTracking()
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == request.NoteId);
        if (source == null)
            return NotFound();
     
        // Owners copying their own note keep the metadata clean (no chip on self-copies)
        var isSelfCopy = source.UserId == userId;
     
        var copy = new Note
        {
            UserId = userId,
            FolderId = null,          // copies land as unfiled in the user's vault
            Title = source.Title,
            Content = source.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CopiedFromUserId = isSelfCopy ? null : source.UserId,
            CopiedFromTitle = isSelfCopy ? null : source.Title
        };
     
        _context.Notes.Add(copy);
        await _context.SaveChangesAsync();
     
        return Json(new { success = true, newNoteId = copy.Id });
    }
 
    // ======= Remove from Shared with me =======
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromSharedWithMe([FromBody] RemoveFromSharedRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
     
        if (request.ItemType == "Note")
        {
            var share = await _context.NoteShares
                .FirstOrDefaultAsync(ns => ns.NoteId == request.ItemId && ns.SharedWithUserId == userId);
            if (share == null)
                return NotFound();
            _context.NoteShares.Remove(share);
        }
        else if (request.ItemType == "Folder")
        {
            var share = await _context.FolderShares
                .FirstOrDefaultAsync(fs => fs.FolderId == request.ItemId && fs.SharedWithUserId == userId);
            if (share == null)
                return NotFound();
            _context.FolderShares.Remove(share);
        }
        else
        {
            return BadRequest(new { error = "Unknown item type." });
        }
     
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    private static SharePermission? ParsePermission(string permission) =>
        Enum.TryParse<SharePermission>(permission, out var parsed) ? parsed : null;
}
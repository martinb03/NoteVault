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

    /// <summary>
    /// Validates a share-change request against the permission rules.
    /// Returns an error message, or null if valid.
    /// </summary>
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

    private static SharePermission? ParsePermission(string permission) =>
        Enum.TryParse<SharePermission>(permission, out var parsed) ? parsed : null;
}
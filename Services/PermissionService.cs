using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;

namespace NoteVault.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;

    public PermissionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EffectivePermission> GetNotePermissionAsync(string userId, int noteId)
    {
        var note = await _context.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note == null)
            return EffectivePermission.None;

        if (note.UserId == userId)
            return EffectivePermission.Owner;

        // Direct note share
        var directShare = await _context.NoteShares
            .AsNoTracking()
            .Where(ns => ns.NoteId == noteId && ns.SharedWithUserId == userId)
            .Select(ns => (SharePermission?)ns.Permission)
            .FirstOrDefaultAsync();

        // Share via containing folder
        SharePermission? folderShare = null;
        if (note.FolderId != null)
        {
            folderShare = await _context.FolderShares
                .AsNoTracking()
                .Where(fs => fs.FolderId == note.FolderId && fs.SharedWithUserId == userId)
                .Select(fs => (SharePermission?)fs.Permission)
                .FirstOrDefaultAsync();
        }

        if (directShare == null && folderShare == null)
            return EffectivePermission.None;

        // Highest wins
        var highest = (SharePermission)Math.Max(
            (int)(directShare ?? SharePermission.View),
            (int)(folderShare ?? SharePermission.View));

        return MapToEffective(highest);
    }

    public async Task<EffectivePermission> GetFolderPermissionAsync(string userId, int folderId)
    {
        var folder = await _context.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null)
            return EffectivePermission.None;

        if (folder.UserId == userId)
            return EffectivePermission.Owner;

        var share = await _context.FolderShares
            .AsNoTracking()
            .Where(fs => fs.FolderId == folderId && fs.SharedWithUserId == userId)
            .Select(fs => (SharePermission?)fs.Permission)
            .FirstOrDefaultAsync();

        return share == null ? EffectivePermission.None : MapToEffective(share.Value);
    }

    private static EffectivePermission MapToEffective(SharePermission p) => p switch
    {
        SharePermission.View => EffectivePermission.View,
        SharePermission.Edit => EffectivePermission.Edit,
        SharePermission.EditAndShare => EffectivePermission.EditAndShare,
        _ => EffectivePermission.None
    };
}
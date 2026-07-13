namespace NoteVault.Services;

public interface IPermissionService
{
    Task<EffectivePermission> GetNotePermissionAsync(string userId, int noteId);
    Task<EffectivePermission> GetFolderPermissionAsync(string userId, int folderId);
}
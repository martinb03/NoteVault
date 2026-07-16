namespace NoteVault.Models;

public class FolderShare
{
    public int Id { get; set; }
    public int FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    public string SharedWithUserId { get; set; } = null!;
    public ApplicationUser SharedWithUser { get; set; } = null!;

    public string SharedByUserId { get; set; } = null!;
    public ApplicationUser SharedByUser { get; set; } = null!;
    
    public SharePermission Permission { get; set; }
    public DateTime CreatedAt { get; set; }
}
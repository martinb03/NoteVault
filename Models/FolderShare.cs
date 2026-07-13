namespace NoteVault.Models;

public class FolderShare
{
    public int Id { get; set; }
    public int FolderId { get; set; }
    public Folder Folder { get; set; }
    
    public string SharedWithUserId { get; set; }
    public ApplicationUser SharedWithUser { get; set; }
    
    public string SharedByUserId { get; set; }
    public ApplicationUser SharedByUser { get; set; }
    
    public SharePermission Permission { get; set; }
    public DateTime CreatedAt { get; set; }
}
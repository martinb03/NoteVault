using Microsoft.AspNetCore.Identity;
namespace NoteVault.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ICollection<Folder> Folders { get; set; } = new List<Folder>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public List<NoteShare> NoteSharesReceived { get; set; } = new();
    public List<NoteShare> NoteSharesInitiated { get; set; } = new();
    public List<FolderShare> FolderSharesReceived { get; set; } = new();
    public List<FolderShare> FolderSharesInitiated { get; set; } = new();
}
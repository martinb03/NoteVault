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
}
namespace NoteVault.Models;

public class Folder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
 
    // Foreign keys
    public string UserId { get; set; } = string.Empty;
 
    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Pile> Piles { get; set; } = new List<Pile>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public List<FolderShare> Shares { get; set; } = new();
}
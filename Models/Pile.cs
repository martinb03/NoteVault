namespace NoteVault.Models;

public class Pile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
 
    // Foreign keys
    public int FolderId { get; set; }
 
    // Navigation
    public Folder Folder { get; set; } = null!;
    public ICollection<PileNote> PileNotes { get; set; } = new List<PileNote>();
}
namespace NoteVault.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
 
    // Foreign keys
    public string UserId { get; set; } = string.Empty;
 
    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
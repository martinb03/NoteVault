namespace NoteVault.Models;

public class NoteVersion
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public int NoteId { get; set; }

    // Navigation
    public Note Note { get; set; } = null!;
}
namespace NoteVault.Models;

public class NoteTag
{
    public int NoteId { get; set; }
    public int TagId { get; set; }

    // Navigation
    public Note Note { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
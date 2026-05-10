namespace NoteVault.Models;

public class PileNote
{
    public int PileId { get; set; }
    public int NoteId { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public Pile Pile { get; set; } = null!;
    public Note Note { get; set; } = null!;
}
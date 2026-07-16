namespace NoteVault.Models;

public class NoteEditLock
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public Note Note { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    
    public DateTime AcquiredAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}
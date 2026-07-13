namespace NoteVault.Models;

public class NoteEditLock
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public Note Note { get; set; }
    
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    
    public DateTime AcquiredAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}
namespace NoteVault.Models;

public class NoteShare
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public Note Note { get; set; }
    
    public string SharedWithUserId { get; set; }
    public ApplicationUser SharedWithUser { get; set; }
    
    public string SharedByUserId { get; set; }
    public ApplicationUser SharedByUser { get; set; }
    
    public SharePermission Permission { get; set; }
    public DateTime CreatedAt { get; set; }
}

namespace NoteVault.Models;
using NpgsqlTypes;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } =  DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    
    // Foreign Keys
    public string UserId { get; set; } = string.Empty;
    public int? FolderId { get; set; }
    
    //Navigation
    public ApplicationUser User { get; set; } = null!;
    public Folder? Folder { get; set; }
    public ICollection<NoteVersion> Versions { get; set; } = new List<NoteVersion>();
    public ICollection<PileNote> PileNotes { get; set; } = new List<PileNote>();
    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
    public NpgsqlTsVector? SearchVector { get; set; }
    public List<NoteShare> Shares { get; set; } = new();
    public NoteEditLock? EditLock { get; set; }
    public string? CopiedFromUserId { get; set; }
    public string? CopiedFromTitle { get; set; }
}
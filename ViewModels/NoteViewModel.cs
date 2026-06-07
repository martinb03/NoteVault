using System.ComponentModel.DataAnnotations;

namespace NoteVault.ViewModels;

// ── Folder dropdown helper ──────────────────────────
public class FolderSelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
 
public class NoteListViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ── Notes list page ─────────────────────────────────
public class NoteListPageViewModel
{
    public List<NoteListViewModel> Notes { get; set; } = new();
    public bool ShowUnfiledOnly { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<FolderSelectItem> AvailableFolders { get; set; } = new();
}
 
// ── Create note (modal) ─────────────────────────────
public class CreateNoteViewModel
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
 
    public int? FolderId { get; set; }
}
 
// ── Note details page ───────────────────────────────
public class NoteDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int VersionCount { get; set; }
    public List<FolderSelectItem> AvailableFolders { get; set; } = new();
}
 
// ── Edit note (Quill editor page) ───────────────────
public class EditNoteViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
    public DateTime UpdatedAt { get; set; }
}
 
// ── Versions list page ──────────────────────────────
public class VersionListPageViewModel
{
    public int NoteId { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public List<VersionListItemViewModel> Versions { get; set; } = new();
    public int?  FolderId { get; set; }
    public string? FolderName { get; set; }
}
 
public class VersionListItemViewModel
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
 
// ── Version details page ────────────────────────────
public class VersionDetailsViewModel
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? Name { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
}
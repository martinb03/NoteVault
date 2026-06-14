namespace NoteVault.ViewModels;
 
public class FolderDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
 
    public List<PileDetailViewModel> PinnedPiles { get; set; } = new();
    public List<PileDetailViewModel> Piles { get; set; } = new();
    public List<NoteListViewModel> Notes { get; set; } = new();
}
 
public class PileDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; }
    public List<PileNoteViewModel> Notes { get; set; } = new();
}
 
public class PileNoteViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<TagListViewModel> Tags { get; set; } = new();
}
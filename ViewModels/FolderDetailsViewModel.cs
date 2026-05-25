namespace NoteVault.ViewModels;

public class FolderDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
 
    public List<PileListViewModel> PinnedPiles { get; set; } = new();
    public List<PileListViewModel> Piles { get; set; } = new();
    public List<NoteListViewModel> Notes { get; set; } = new();
}

public class PileListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsPinned { get; set; }
    public int NoteCount { get; set; }
}

public class NoteListViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
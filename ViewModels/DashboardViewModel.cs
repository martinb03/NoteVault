namespace NoteVault.ViewModels;
 
public class DashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEmpty { get; set; }
 
    public List<DashboardNoteDto> RecentlyOpened { get; set; } = new();
    public List<DashboardNoteDto> FrequentlyVisited { get; set; } = new();
    public List<DashboardNoteDto> RecentlyCreated { get; set; } = new();
    public DashboardNoteDto? Random { get; set; }
}
 
public class DashboardNoteDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public string? FolderName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int ViewCount { get; set; }
    public List<TagListViewModel> Tags { get; set; } = new();
}
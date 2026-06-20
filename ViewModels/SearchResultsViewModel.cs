namespace NoteVault.ViewModels;
 
public class SearchResultRowDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FolderName { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public List<TagListViewModel> Tags { get; set; } = new();
}
 
public class SearchResultsViewModel
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResultRowDto> Results { get; set; } = new();
 
    // Current filter state
    public int? FolderId { get; set; }
    public List<int> TagIds { get; set; } = new();
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string DateType { get; set; } = "updated"; // "created" or "updated"
    public string Sort { get; set; } = "relevance"; // "relevance" or "updated"
 
    // For the sidebar filter UI
    public List<FolderSelectItem> AvailableFolders { get; set; } = new();
    public List<TagListViewModel> AvailableTags { get; set; } = new();
}
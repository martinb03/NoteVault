namespace NoteVault.ViewModels;
 
public class SearchResultDto
{
    public int Id { get; set; }
    public string TitleHighlight { get; set; } = string.Empty;
    public string? FolderName { get; set; }
    public string Snippet { get; set; } = string.Empty;
}
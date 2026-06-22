namespace NoteVault.ViewModels;

public class NoteExportModel
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
 
public class PileExportModel
{
    public string PileName { get; set; } = string.Empty;
    public List<NoteExportModel> Notes { get; set; } = new();
}
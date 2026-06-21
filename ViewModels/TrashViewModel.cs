namespace NoteVault.ViewModels;
 
public class TrashViewModel
{
    public string Tab { get; set; } = "folders"; // "folders" | "piles" | "notes"
 
    public List<TrashFolderDto> Folders { get; set; } = new();
    public List<TrashPileDto> Piles { get; set; } = new();
    public List<TrashNoteDto> Notes { get; set; } = new();
}
 
public class TrashFolderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
    public List<TrashFolderPileDto> Piles { get; set; } = new();
    public List<TrashFolderNoteDto> Notes { get; set; } = new();
}
 
public class TrashFolderPileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
}
 
public class TrashFolderNoteDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
}
 
public class TrashPileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string? FolderName { get; set; }
    public bool ParentFolderDeleted { get; set; }
    public DateTime DeletedAt { get; set; }
}
 
public class TrashNoteDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FolderName { get; set; }
    public DateTime DeletedAt { get; set; }
}
using System.ComponentModel.DataAnnotations;
 
namespace NoteVault.ViewModels;
 
public class CreatePileViewModel
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
 
    public int FolderId { get; set; }
 
    public string? Color { get; set; }
    public string? Icon { get; set; }
}
 
public class EditPileViewModel
{
    public int Id { get; set; }
 
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
 
    public string? Color { get; set; }
    public string? Icon { get; set; }
}
 
public class ManageNotesRequest
{
    public int PileId { get; set; }
    public List<int> NoteIds { get; set; } = new();
}
 
public class ReorderRequest
{
    public int Id { get; set; }
    public string Direction { get; set; } = "up"; // "up" or "down"
}
 
public class ReorderNoteInPileRequest
{
    public int PileId { get; set; }
    public int NoteId { get; set; }
    public string Direction { get; set; } = "up";
}
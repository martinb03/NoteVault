using System.ComponentModel.DataAnnotations;

namespace NoteVault.ViewModels;

public class CreateFolderViewModel
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
 
    [MaxLength(500)]
    public string? Description { get; set; }
}

public class EditFolderViewModel
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
 
    [MaxLength(500)]
    public string? Description { get; set; }
}

public class FolderListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int NoteCount { get; set; }
    public int PileCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
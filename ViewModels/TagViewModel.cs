using System.ComponentModel.DataAnnotations;
 
namespace NoteVault.ViewModels;
 
public class CreateTagViewModel
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
 
    public string? Color { get; set; }
}
 
public class EditTagViewModel
{
    public int Id { get; set; }
 
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
 
    public string? Color { get; set; }
}
 
public class TagListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int NoteCount { get; set; }
}

public class TagDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public List<NoteListViewModel> Notes { get; set; } = new();
}
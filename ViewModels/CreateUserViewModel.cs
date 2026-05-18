using System.ComponentModel.DataAnnotations;

namespace NoteVault.ViewModels;

public class CreateUserViewModel
{
    [Required]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;
 
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
 
    [Required]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;
 
    [Required]
    public string Role { get; set; } = "User";
}
namespace NoteVault.ViewModels;
using System.ComponentModel.DataAnnotations;

public class SetupViewModel
{
    [Required]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;
 
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
 
    [Required]
    [DataType(DataType.Password)]
    [MinLength(10, ErrorMessage = "Password must be at least 10 characters.")]
    public string Password { get; set; } = string.Empty;
 
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

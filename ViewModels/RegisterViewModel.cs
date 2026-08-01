using System.ComponentModel.DataAnnotations;

namespace NoteVault.ViewModels;

public class RegisterViewModel
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
}
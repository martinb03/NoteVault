using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;

namespace NoteVault.Controllers;

public class SettingsController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;

    public SettingsController(
        UserManager<ApplicationUser> userManager, 
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _context = context;
    }
    
    // ── Main settings page ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var settings = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        ViewData["IsRegistrationOpen"] = settings?.IsRegistrationOpen ?? false;
        if (user == null) return RedirectToAction("Login", "Account");

        
        
        var model = new AccountSettingsViewModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty
        };
 
        return View(model);
    }
    
    // ---------------- Update display name ----------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDisplayName(string displayName)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
 
        user.DisplayName = displayName;
        var result = await _userManager.UpdateAsync(user);
 
        if (result.Succeeded)
        {
            // Update the claim in the cookie
            var existingClaim = (await _userManager.GetClaimsAsync(user))
                .FirstOrDefault(c => c.Type == "DisplayName");
            if (existingClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, existingClaim);
            }
            await _userManager.AddClaimAsync(user, new Claim("DisplayName", displayName));
 
            // Refresh the sign-in so the cookie reflects the change
            await _signInManager.RefreshSignInAsync(user);
 
            TempData["Success"] = "Display name updated.";
        }
 
        return RedirectToAction("Index");
    }
    
    // ---------------- Update email ----------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmail(string email)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
 
        user.Email = email;
        user.UserName = email;
        var result = await _userManager.UpdateAsync(user);
 
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Email updated.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }
 
        return RedirectToAction("Index");
    }
    
    // ---------------- Change password ---------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fill in all password fields correctly.";
            return RedirectToAction("Index");
        }
 
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
 
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
 
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Password changed.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }
 
        return RedirectToAction("Index");
    }
    
    // ---------------- Admin: List all users ----------------
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users()
    {
        var users = _userManager.Users.ToList();
        var userList = new List<UserListViewModel>();
 
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userList.Add(new UserListViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "User",
                CreatedAt = user.CreatedAt,
                IsLockedOut = await _userManager.IsLockedOutAsync(user)
            });
        }
 
        return Json(userList);
    }
    
    // ---------------- Admin: Create user ----------------
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fill in all fields correctly.";
            return RedirectToAction("Index");
        }
 
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName
        };
 
        var result = await _userManager.CreateAsync(user, model.Password);
 
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, model.Role);
            await _userManager.AddClaimAsync(user, new Claim("DisplayName", model.DisplayName));
            TempData["Success"] = $"User {model.DisplayName} created.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }
 
        return RedirectToAction("Index");
    }
    
    // ---------------- Admin: Reset user password ----------------
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index");
        }
 
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
 
        if (result.Succeeded)
        {
            TempData["Success"] = $"Password reset for {user.DisplayName}.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }
 
        return RedirectToAction("Index");
    }
    
    // ---------------- Admin: Toggle user enabled/disabled ----------------
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index");
        }
 
        if (await _userManager.IsLockedOutAsync(user))
        {
            // Unlock the user
            await _userManager.SetLockoutEndDateAsync(user, null);
            TempData["Success"] = $"{user.DisplayName} has been enabled.";
        }
        else
        {
            // Lock the user out indefinitely
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            TempData["Success"] = $"{user.DisplayName} has been disabled.";
        }
 
        return RedirectToAction("Index");
    }
    
    // ---------------- Admin: Delete user ----------------
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var user = await _userManager.FindByIdAsync(userId);
 
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index");
        }
 
        // Prevent admin from deleting themselves
        if (user.Id == currentUser?.Id)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction("Index");
        }
 
        var result = await _userManager.DeleteAsync(user);
 
        if (result.Succeeded)
        {
            TempData["Success"] = $"User {user.DisplayName} has been deleted.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }
 
        return RedirectToAction("Index");
    }
    
    // ------- Admin: Allow open registration -------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetRegistrationOpen([FromForm] bool isOpen)
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings == null)
        {
            return Json(new { success = false });
        }
        settings.IsRegistrationOpen = isOpen;
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
}
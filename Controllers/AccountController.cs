using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NoteVault.Models;
using NoteVault.ViewModels;

namespace NoteVault.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Login()
    {
        // If no users exist, redirect to setup
        if (!_userManager.Users.Any())
        {
            return RedirectToAction("Index", "Setup");
        }
 
        // If already logged in, go to home
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
 
        return View(new LoginViewModel());
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
 
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);
 
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("DisplayName", user.DisplayName));
            }

            return RedirectToAction("Index", "Home");
        }
 
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }
}
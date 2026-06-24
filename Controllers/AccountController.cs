using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;


namespace NoteVault.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
 
    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AppDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
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
 
        var settings = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        ViewData["IsRegistrationOpen"] = settings?.IsRegistrationOpen ?? false;
        
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

            return RedirectToAction("Index", "Dashboard");
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
    
    [HttpGet]
    public async Task<IActionResult> Register()
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings == null || !settings.IsRegistrationOpen)
        {
            return RedirectToAction("Login");
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings == null || !settings.IsRegistrationOpen)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return View(model);
    }
}
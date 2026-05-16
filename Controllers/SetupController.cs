using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NoteVault.Models;
using NoteVault.ViewModels;

namespace NoteVault.Controllers;

public class SetupController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public SetupController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // If users already exist, setup is locked — redirect to login
        if (_userManager.Users.Any())
        {
            return RedirectToAction("Login", "Account");
        }
 
        return View(new SetupViewModel());
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SetupViewModel model)
    {
        // Double-check: if users already exist, block setup
        if (_userManager.Users.Any())
        {
            return RedirectToAction("Login", "Account");
        }
 
        if (!ModelState.IsValid)
        {
            return View(model);
        }
 
        // Create the Admin role if it doesn't exist
        if (!await _roleManager.RoleExistsAsync("Admin"))
        {
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
        }
 
        // Create the User role as well
        if (!await _roleManager.RoleExistsAsync("User"))
        {
            await _roleManager.CreateAsync(new IdentityRole("User"));
        }
 
        // Create the admin user
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName
        };
 
        var result = await _userManager.CreateAsync(user, model.Password);
 
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }
 
        // If creation failed, show the errors
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
 
        return View(model);
    }
}
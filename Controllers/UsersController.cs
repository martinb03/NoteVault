using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Models;
using NoteVault.ViewModels;

namespace NoteVault.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    // ======= Type-ahead search =======

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        // Minimum 2 characters; return empty list for shorter queries.
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(Array.Empty<UserSearchResultViewModel>());

        var currentUserId = _userManager.GetUserId(User)!;
        var query = q.Trim().ToLower();
        var now = DateTimeOffset.UtcNow;

        var results = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Id != currentUserId)
            .Where(u => u.LockoutEnd == null || u.LockoutEnd <= now)
            .Where(u =>
                u.DisplayName.ToLower().Contains(query) ||
                (u.Email != null && u.Email.ToLower().Contains(query)))
            .OrderBy(u => u.DisplayName)
            .Take(10)
            .Select(u => new UserSearchResultViewModel
            {
                UserId = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email ?? ""
            })
            .ToListAsync();

        return Json(results);
    }
}
using System.Diagnostics;
using DocVault.Database;
using Microsoft.AspNetCore.Mvc;
using DocVault.Models;

namespace DocVault.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    private readonly AppDbContext _context;
    
    public HomeController(ILogger<HomeController> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Folders()
    {
        return View();
    }

    public IActionResult Tags()
    {
        return View();
    }

    public IActionResult Notes()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
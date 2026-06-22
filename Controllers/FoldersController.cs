using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.Services;
using NoteVault.ViewModels;

namespace NoteVault.Controllers;

[Authorize]
public class FoldersController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RazorViewRenderer _viewRenderer;
    private readonly PdfService _pdfService;
    
    public FoldersController(AppDbContext context, 
        UserManager<ApplicationUser> userManager,
        RazorViewRenderer viewRenderer,
        PdfService pdfService)
    {
        _context = context;
        _userManager = userManager;
        _viewRenderer = viewRenderer;
        _pdfService = pdfService;
    }
    
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
 
        var folders = await _context.Folders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => new FolderListViewModel
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                NoteCount = f.Notes.Count(),
                PileCount = f.Piles.Count(),
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();
 
        return View(folders);
    }
    
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateFolderViewModel());
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFolderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
 
        var userId = _userManager.GetUserId(User);
 
        // Get the next sort order
        var maxSortOrder = await _context.Folders
            .Where(f => f.UserId == userId)
            .MaxAsync(f => (int?)f.SortOrder) ?? 0;
 
        var folder = new Folder
        {
            Name = model.Name,
            Description = model.Description,
            UserId = userId!,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
 
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
 
        return RedirectToAction("Index");
    }
    
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        var model = new EditFolderViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description
        };
 
        return View(model);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditFolderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
 
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == model.Id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        folder.Name = model.Name;
        folder.Description = model.Description;
        folder.UpdatedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        return RedirectToAction("Details", new{id = model.Id});
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .Include(f => f.Piles)
            .Include(f => f.Notes)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        var now = DateTime.UtcNow;
 
        // Soft delete all notes in this folder
        foreach (var note in folder.Notes)
        {
            note.DeletedAt = now;
        }
 
        // Soft delete all piles in this folder
        foreach (var pile in folder.Piles)
        {
            pile.DeletedAt = now;
        }
 
        // Soft delete the folder itself
        folder.DeletedAt = now;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Folder \"{folder.Name}\" and its contents moved to trash.";
        return RedirectToAction("Index");
    }
    
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);
 
        var folder = await _context.Folders
            .Include(f => f.Piles)
                .ThenInclude(p => p.PileNotes)
                    .ThenInclude(pn => pn.Note)
                        .ThenInclude(n=>n.NoteTags)
                            .ThenInclude(nt=>nt.Tag)
            .Include(f => f.Notes)
                .ThenInclude(n => n.NoteTags)
                    .ThenInclude(nt => nt.Tag)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
 
        if (folder == null) return NotFound();
 
        // Collect all note IDs that are in any pile (to exclude from loose notes)
        var pileNoteIds = folder.Piles
            .SelectMany(p => p.PileNotes)
            .Select(pn => pn.NoteId)
            .ToHashSet();
 
        PileDetailViewModel MapPile(Pile p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Color = p.Color,
            Icon = p.Icon,
            IsPinned = p.IsPinned,
            SortOrder = p.SortOrder,
            Notes = p.PileNotes
                .OrderBy(pn => pn.SortOrder)
                .Select(pn => new PileNoteViewModel
                {
                    Id = pn.NoteId,
                    Title = pn.Note.Title,
                    SortOrder = pn.SortOrder,
                    Tags = pn.Note.NoteTags
                        .Select(nt => new TagListViewModel
                        {
                            Id = nt.Tag.Id,
                            Name = nt.Tag.Name,
                            Color = nt.Tag.Color
                        })
                        .OrderBy(t=>t.Name)
                        .ToList()
                })
                .ToList()
        };
 
        var model = new FolderDetailsViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description,
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt,
            PinnedPiles = folder.Piles
                .Where(p => p.IsPinned)
                .OrderBy(p => p.SortOrder)
                .Select(MapPile)
                .ToList(),
            Piles = folder.Piles
                .Where(p => !p.IsPinned)
                .OrderBy(p => p.SortOrder)
                .Select(MapPile)
                .ToList(),
            Notes = folder.Notes
                .Where(n => !pileNoteIds.Contains(n.Id))
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteListViewModel
                {
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt,
                    Tags = n.NoteTags
                        .Select(nt => new TagListViewModel
                        {
                            Id = nt.Tag.Id,
                            Name = nt.Tag.Name,
                            Color = nt.Tag.Color
                        })
                        .OrderBy(t => t.Name)
                        .ToList()
                })
                .ToList()
        };
 
        return View(model);
    }
    
    [HttpGet]
    public async Task<IActionResult> ListJson()
    {
        var userId = _userManager.GetUserId(User);
        var folders = await _context.Folders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new { id = f.Id, name = f.Name })
            .ToListAsync();
        return Json(folders);
    }
    
    [HttpGet]
    public async Task<IActionResult> Export(int id)
    {
        var userId = _userManager.GetUserId(User);

        var folder = await _context.Folders
            .Include(f => f.Notes)
            .Include(f => f.Piles)
                .ThenInclude(p => p.PileNotes.OrderBy(pn => pn.SortOrder))
                    .ThenInclude(pn => pn.Note)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (folder == null) return NotFound();

        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // Loose notes (not in any pile) — root level
            var pileNoteIds = folder.Piles.SelectMany(p => p.PileNotes).Select(pn => pn.NoteId).ToHashSet();
            var looseNotes = folder.Notes.Where(n => !pileNoteIds.Contains(n.Id)).ToList();

            foreach (var note in looseNotes)
            {
                var html = await _viewRenderer.RenderAsync("/Views/Shared/Export/_NoteExport.cshtml",
                    new NoteExportModel { Title = note.Title, Content = note.Content });
                var pdfBytes = await _pdfService.HtmlToPdfAsync(html);
                var entryName = $"{FileNameSanitizer.Sanitize(note.Title)}.pdf";

                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdfBytes);
            }

            // Piles — each becomes a subfolder containing its notes as PDFs
            foreach (var pile in folder.Piles)
            {
                var pileFolder = FileNameSanitizer.Sanitize(pile.Name);

                foreach (var pn in pile.PileNotes)
                {
                    var html = await _viewRenderer.RenderAsync("/Views/Shared/Export/_NoteExport.cshtml",
                        new NoteExportModel { Title = pn.Note.Title, Content = pn.Note.Content });
                    var pdfBytes = await _pdfService.HtmlToPdfAsync(html);
                    var entryName = $"{pileFolder}/{FileNameSanitizer.Sanitize(pn.Note.Title)}.pdf";

                    var entry = archive.CreateEntry(entryName);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pdfBytes);
                }
            }
        }

        memoryStream.Position = 0;
        var zipFilename = $"{FileNameSanitizer.Sanitize(folder.Name)}.zip";
        return File(memoryStream.ToArray(), "application/zip", zipFilename);
    }
}
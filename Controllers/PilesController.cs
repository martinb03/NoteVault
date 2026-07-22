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
public class PilesController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RazorViewRenderer _viewRenderer;
    private readonly PdfService _pdfService;
    private readonly IPermissionService _permissionService;
 
    public PilesController(AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RazorViewRenderer viewRenderer,
        PdfService pdfService,
        IPermissionService permissionService)
    {
        _context = context;
        _userManager = userManager;
        _viewRenderer = viewRenderer;
        _pdfService = pdfService;
        _permissionService = permissionService;
    }
 
    // ========= Create Pile =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePileViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetFolderPermissionAsync(userId, model.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        var folderExists = await _context.Folders.AnyAsync(f => f.Id == model.FolderId);
        if (!folderExists) return NotFound();
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Pile name is required.";
            return RedirectToAction("Details", "Folders", new { id = model.FolderId });
        }
 
        var maxSortOrder = await _context.Piles
            .Where(p => p.FolderId == model.FolderId)
            .MaxAsync(p => (int?)p.SortOrder) ?? 0;
 
        var pile = new Pile
        {
            Name = model.Name,
            FolderId = model.FolderId,
            Color = model.Color,
            Icon = model.Icon,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
 
        _context.Piles.Add(pile);
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Pile \"{pile.Name}\" created.";
        return RedirectToAction("Details", "Folders", new { id = model.FolderId });
    }
 
    // ========= Edit Pile =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditPileViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
 
        var pile = await _context.Piles
            .FirstOrDefaultAsync(p => p.Id == model.Id);
 
        if (pile == null) return NotFound();
 
        var permission = await _permissionService.GetFolderPermissionAsync(userId, pile.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Pile name is required.";
            return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
        }
 
        pile.Name = model.Name;
        pile.Color = model.Color;
        pile.Icon = model.Icon;
        pile.UpdatedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = "Pile updated.";
        return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
    }
 
    // ========= Toggle Pin =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var userId = _userManager.GetUserId(User)!;
 
        var pile = await _context.Piles
            .FirstOrDefaultAsync(p => p.Id == id);
 
        if (pile == null) return NotFound();
 
        var permission = await _permissionService.GetFolderPermissionAsync(userId, pile.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        pile.IsPinned = !pile.IsPinned;
        pile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
 
        TempData["Success"] = pile.IsPinned ? "Pile pinned." : "Pile unpinned.";
        return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
    }
 
    // ========= Manage Notes =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageNotes(ManageNotesRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
     
        var pile = await _context.Piles
            .Include(p => p.PileNotes)
            .FirstOrDefaultAsync(p => p.Id == request.PileId);
     
        if (pile == null) return NotFound();
     
        var permission = await _permissionService.GetFolderPermissionAsync(userId, pile.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
     
        // Candidate notes: everything in the same folder, regardless of note ownership.
        // (In shared folders, notes belong to the folder owner — access is via the folder.)
        var folderNoteIds = await _context.Notes
            .Where(n => n.FolderId == pile.FolderId)
            .Select(n => n.Id)
            .ToListAsync();
     
        var validIds = request.NoteIds
            .Where(id => folderNoteIds.Contains(id))
            .ToHashSet();
     
        var currentIds = pile.PileNotes.Select(pn => pn.NoteId).ToHashSet();
     
        var toRemove = pile.PileNotes.Where(pn => !validIds.Contains(pn.NoteId)).ToList();
        _context.PileNotes.RemoveRange(toRemove);
     
        var toAdd = validIds.Except(currentIds).ToList();
        if (toAdd.Any())
        {
            var crossPileAssociations = await _context.PileNotes
                .Where(pn => toAdd.Contains(pn.NoteId) && pn.Pile.FolderId == pile.FolderId && pn.PileId != pile.Id)
                .ToListAsync();
            _context.PileNotes.RemoveRange(crossPileAssociations);
     
            var maxSortOrder = pile.PileNotes.Any() ? pile.PileNotes.Max(pn => pn.SortOrder) : 0;
            foreach (var noteId in toAdd)
            {
                maxSortOrder++;
                _context.PileNotes.Add(new PileNote
                {
                    PileId = pile.Id,
                    NoteId = noteId,
                    SortOrder = maxSortOrder
                });
            }
        }
     
        await _context.SaveChangesAsync();
     
        TempData["Success"] = "Pile notes updated.";
        return RedirectToAction("Details", "Folders", new { id = pile.FolderId });
    }
 
    // ========= Reorder Pile (up/down) =========
    public class SavePileOrderRequest
    {
        public int FolderId { get; set; }
        public List<int> PileIds { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> SavePileOrder([FromBody] SavePileOrderRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
        var permission = await _permissionService.GetFolderPermissionAsync(userId, request.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        var piles = await _context.Piles
            .Where(p => p.FolderId == request.FolderId)
            .ToListAsync();
 
        if (!piles.Any()) return NotFound();
 
        for (int i = 0; i < request.PileIds.Count; i++)
        {
            var pile = piles.FirstOrDefault(p => p.Id == request.PileIds[i]);
            if (pile != null) pile.SortOrder = i + 1;
        }
 
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
 
    // ========= Reorder Note in Pile =========
    public class SaveNoteOrderRequest
    {
        public int PileId { get; set; }
        public List<int> NoteIds { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> SaveNoteOrder([FromBody] SaveNoteOrderRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
 
        var pile = await _context.Piles
            .Include(p => p.PileNotes)
            .FirstOrDefaultAsync(p => p.Id == request.PileId);
 
        if (pile == null) return NotFound();
 
        var permission = await _permissionService.GetFolderPermissionAsync(userId, pile.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        for (int i = 0; i < request.NoteIds.Count; i++)
        {
            var pn = pile.PileNotes.FirstOrDefault(p => p.NoteId == request.NoteIds[i]);
            if (pn != null) pn.SortOrder = i + 1;
        }
 
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
 
    // ========= Soft Delete Pile =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User)!;
 
        var pile = await _context.Piles
            .Include(p => p.PileNotes)
            .FirstOrDefaultAsync(p => p.Id == id);
 
        if (pile == null) return NotFound();
 
        var permission = await _permissionService.GetFolderPermissionAsync(userId, pile.FolderId);
        if (permission < EffectivePermission.Edit)
            return Forbid();
 
        var folderId = pile.FolderId;
 
        pile.DeletedAt = DateTime.UtcNow;
 
        await _context.SaveChangesAsync();
 
        TempData["Success"] = $"Pile \"{pile.Name}\" moved to trash.";
        return RedirectToAction("Details", "Folders", new { id = folderId });
    }
    
    // ====== Export PIle ======
    [HttpGet]
    public async Task<IActionResult> Export(int id)
    {
        var userId = _userManager.GetUserId(User)!;
 
        var pile = await _context.Piles
            .Include(p => p.PileNotes.OrderBy(pn => pn.SortOrder))
            .ThenInclude(pn => pn.Note)
            .FirstOrDefaultAsync(p => p.Id == id);
 
        if (pile == null) return NotFound();
 
        var permission = await _permissionService.GetFolderPermissionAsync(userId, pile.FolderId);
        if (permission == EffectivePermission.None)
            return Forbid();
 
        var model = new PileExportModel
        {
            PileName = pile.Name,
            Notes = pile.PileNotes
                .Select(pn => new NoteExportModel
                {
                    Title = pn.Note.Title,
                    Content = pn.Note.Content
                })
                .ToList()
        };
 
        var html = await _viewRenderer.RenderAsync("/Views/Shared/Export/_PileExport.cshtml", model);
        var pdfBytes = await _pdfService.HtmlToPdfAsync(html);
        var filename = $"{FileNameSanitizer.Sanitize(pile.Name)}.pdf";
 
        return File(pdfBytes, "application/pdf", filename);
    }
}
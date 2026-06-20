using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using NoteVault.Database;
using NoteVault.Models;
using NoteVault.ViewModels;
 
namespace NoteVault.Controllers;
 
[Authorize]
public class SearchController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
 
    public SearchController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
 
    [HttpGet]
    public async Task<IActionResult> LiveSearch(string? q, List<int>? tagIds = null, int? folderId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new List<SearchResultDto>());
        }
 
        var tsQueryString = BuildPrefixTsQuery(q);
        if (string.IsNullOrWhiteSpace(tsQueryString))
        {
            return Json(new List<SearchResultDto>());
        }
 
        var userId = _userManager.GetUserId(User);
        var tagIdsArray = tagIds?.ToArray() ?? Array.Empty<int>();
 
        var sql = @"
SELECT
    n.""Id"" AS ""Id"",
    ts_headline('english', n.""Title"", to_tsquery('english', @q), 'HighlightAll=true') AS ""TitleHighlight"",
    f.""Name"" AS ""FolderName"",
    ts_headline('english', regexp_replace(coalesce(n.""Content"", ''), '<[^>]*>', ' ', 'g'), to_tsquery('english', @q), 'MaxWords=20, MinWords=10') AS ""Snippet""
FROM ""Notes"" n
LEFT JOIN ""Folders"" f ON f.""Id"" = n.""FolderId""
WHERE n.""UserId"" = @userId
  AND n.""DeletedAt"" IS NULL
  AND n.""SearchVector"" @@ to_tsquery('english', @q)
  AND (
    cardinality(@tagIds) = 0
    OR n.""Id"" IN (
        SELECT ""NoteId"" FROM ""NoteTags""
        WHERE ""TagId"" = ANY(@tagIds)
        GROUP BY ""NoteId""
        HAVING COUNT(DISTINCT ""TagId"") = cardinality(@tagIds)
    )
  )
  AND (@folderId IS NULL OR n.""FolderId"" = @folderId)
ORDER BY ts_rank(n.""SearchVector"", to_tsquery('english', @q)) DESC
LIMIT 3
";
 
        var qParam = new NpgsqlParameter("q", tsQueryString);
        var userIdParam = new NpgsqlParameter("userId", userId);
        var tagIdsParam = new NpgsqlParameter("tagIds", NpgsqlDbType.Array | NpgsqlDbType.Integer)
        {
            Value = tagIdsArray
        };
        var folderIdParam = new NpgsqlParameter("folderId", NpgsqlDbType.Integer)
        {
            Value = folderId.HasValue ? (object)folderId.Value : DBNull.Value
        };
 
        var results = await _context.Database
            .SqlQueryRaw<SearchResultDto>(sql, qParam, userIdParam, tagIdsParam, folderIdParam)
            .ToListAsync();
 
        return Json(results);
    }
 
    private static string BuildPrefixTsQuery(string input)
    {
        var terms = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
            .Where(word => word.Length > 0)
            .Select(word => word + ":*");
        return string.Join(" & ", terms);
    }
    
}
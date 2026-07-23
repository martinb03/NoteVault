using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
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
WHERE (
    n.""UserId"" = @userId
    OR EXISTS (
        SELECT 1 FROM ""NoteShares"" ns
        WHERE ns.""NoteId"" = n.""Id"" AND ns.""SharedWithUserId"" = @userId
    )
    OR (n.""FolderId"" IS NOT NULL AND EXISTS (
        SELECT 1 FROM ""FolderShares"" fs
        WHERE fs.""FolderId"" = n.""FolderId"" AND fs.""SharedWithUserId"" = @userId
    ))
)
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
    
    // ====== Results page ======
    [HttpGet]
    public async Task<IActionResult> Results(
        string? q,
        List<int>? tagIds = null,
        int? folderId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string dateType = "updated",
        string sort = "relevance")
    {
        var userId = _userManager.GetUserId(User);
 
        var results = await RunSearch(q, tagIds, folderId, dateFrom, dateTo, dateType, sort, userId!);
 
        var availableFolders = await _context.Folders
            .Where(f => f.UserId == userId
                        || _context.FolderShares.Any(fs => fs.FolderId == f.Id && fs.SharedWithUserId == userId))
            .OrderBy(f => f.Name)
            .Select(f => new FolderSelectItem { Id = f.Id, Name = f.Name })
            .ToListAsync();
 
        var availableTags = await _context.Tags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TagListViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .ToListAsync();
 
        var model = new SearchResultsViewModel
        {
            Query = q ?? "",
            Results = results,
            FolderId = folderId,
            TagIds = tagIds ?? new List<int>(),
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateType = dateType,
            Sort = sort,
            AvailableFolders = availableFolders,
            AvailableTags = availableTags
        };
        ViewData["SearchUrl"] = HttpContext.Request.GetEncodedPathAndQuery();
        return View(model);
    }
 
    // ====== Partial for AJAX filter updates ======
    [HttpGet]
    public async Task<IActionResult> ResultsPartial(
        string? q,
        List<int>? tagIds = null,
        int? folderId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string dateType = "updated",
        string sort = "relevance")
    {
        var userId = _userManager.GetUserId(User);
        var results = await RunSearch(q, tagIds, folderId, dateFrom, dateTo, dateType, sort, userId!);
        ViewData["SearchUrl"] = "/Search/Results" + HttpContext.Request.QueryString.ToString();
        return PartialView("_ResultsList", results);
    }
 
    // ====== Shared search logic ======
    private async Task<List<SearchResultRowDto>> RunSearch(
        string? q,
        List<int>? tagIds,
        int? folderId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string dateType,
        string sort,
        string userId)
    {
        var tagIdsArray = tagIds?.ToArray() ?? Array.Empty<int>();
        var dateColumn = dateType == "created" ? "\"CreatedAt\"" : "\"UpdatedAt\"";
 
        var hasQuery = !string.IsNullOrWhiteSpace(q);
        var tsQueryString = hasQuery ? BuildPrefixTsQuery(q!) : "";
        if (hasQuery && string.IsNullOrWhiteSpace(tsQueryString))
        {
            return new List<SearchResultRowDto>();
        }
 
        // ORDER BY changes based on sort and whether we have a text query
        string orderBy;
        if (sort == "updated")
        {
            orderBy = "n.\"UpdatedAt\" DESC";
        }
        else if (hasQuery)
        {
            orderBy = "ts_rank(n.\"SearchVector\", to_tsquery('english', @q)) DESC";
        }
        else
        {
            orderBy = "n.\"UpdatedAt\" DESC";
        }
 
        // Title and snippet vary depending on whether there's a search query
        var titleExpr = hasQuery
            ? "ts_headline('english', n.\"Title\", to_tsquery('english', @q), 'HighlightAll=true')"
            : "n.\"Title\"";
        var snippetExpr = hasQuery
            ? "ts_headline('english', regexp_replace(coalesce(n.\"Content\", ''), '<[^>]*>', ' ', 'g'), to_tsquery('english', @q), 'MaxWords=25, MinWords=15')"
            : "left(regexp_replace(coalesce(n.\"Content\", ''), '<[^>]*>', ' ', 'g'), 150)";
        var matchClause = hasQuery
            ? "AND n.\"SearchVector\" @@ to_tsquery('english', @q)"
            : "";
 
        var sql = $@"
SELECT
    n.""Id"" AS ""Id"",
    {titleExpr} AS ""Title"",
    f.""Name"" AS ""FolderName"",
    {snippetExpr} AS ""Snippet"",
    n.""UpdatedAt"" AS ""UpdatedAt"",
    u.""DisplayName"" AS ""OwnerName"",
    (n.""UserId"" <> @userId) AS ""IsShared""
FROM ""Notes"" n
LEFT JOIN ""Folders"" f ON f.""Id"" = n.""FolderId""
LEFT JOIN ""AspNetUsers"" u ON u.""Id"" = n.""UserId""
WHERE (
    n.""UserId"" = @userId
    OR EXISTS (
        SELECT 1 FROM ""NoteShares"" ns
        WHERE ns.""NoteId"" = n.""Id"" AND ns.""SharedWithUserId"" = @userId
    )
    OR (n.""FolderId"" IS NOT NULL AND EXISTS (
        SELECT 1 FROM ""FolderShares"" fs
        WHERE fs.""FolderId"" = n.""FolderId"" AND fs.""SharedWithUserId"" = @userId
    ))
)
  AND n.""DeletedAt"" IS NULL
  {matchClause}
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
  AND (@dateFrom IS NULL OR n.{dateColumn} >= @dateFrom)
  AND (@dateTo IS NULL OR n.{dateColumn} <= @dateTo)
ORDER BY {orderBy}
LIMIT 50
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
        var dateFromParam = new NpgsqlParameter("dateFrom", NpgsqlDbType.TimestampTz)
        {
            Value = dateFrom.HasValue ? (object)DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DBNull.Value
        };
        var dateToParam = new NpgsqlParameter("dateTo", NpgsqlDbType.TimestampTz)
        {
            Value = dateTo.HasValue ? (object)DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DBNull.Value
        };
 
        var rawResults = await _context.Database
            .SqlQueryRaw<SearchRowRawDto>(sql,
                qParam, userIdParam, tagIdsParam, folderIdParam, dateFromParam, dateToParam)
            .ToListAsync();
 
        // Fetch tags for all returned notes in one query
        var noteIds = rawResults.Select(r => r.Id).ToList();
        var tagsByNote = await _context.NoteTags
            .Include(nt => nt.Tag)
            .Where(nt => noteIds.Contains(nt.NoteId) && nt.Tag.UserId == userId)   // Current user filter
            .ToListAsync();
 
        var tagMap = tagsByNote
            .GroupBy(nt => nt.NoteId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(nt => new TagListViewModel
                {
                    Id = nt.Tag.Id,
                    Name = nt.Tag.Name,
                    Color = nt.Tag.Color
                }).OrderBy(t => t.Name).ToList()
            );
 
        return rawResults.Select(r => new SearchResultRowDto
        {
            Id = r.Id,
            Title = r.Title,
            FolderName = r.FolderName,
            Snippet = r.Snippet ?? "",
            UpdatedAt = r.UpdatedAt,
            OwnerName =  r.OwnerName,
            IsShared = r.IsShared,
            Tags = tagMap.TryGetValue(r.Id, out var tags) ? tags : new List<TagListViewModel>()
        }).ToList();
    }
 
    private class SearchRowRawDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? FolderName { get; set; }
        public string? Snippet { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public bool IsShared { get; set; }
    }
    
}
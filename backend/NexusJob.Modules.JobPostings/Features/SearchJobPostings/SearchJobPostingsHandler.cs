using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Contracts;
using NexusJob.Modules.JobPostings.Persistence;

namespace NexusJob.Modules.JobPostings.Features.SearchJobPostings;

/// <summary>
/// Searches job postings by an optional keyword (FR-4). A case-insensitive
/// <c>ILIKE</c> substring match over title OR description, run in the database
/// (AD-18) - never a search engine or index service. A missing/empty
/// <c>query</c> matches every row, so a plain browse listing and a keyword
/// search are the same request (spec Intent).
///
/// Company names are resolved through exactly one batched
/// <see cref="IIdentityApi.GetCompanies"/> call for the whole page (AD-5 / AD-19)
/// - never a per-row lookup. A row whose owner is unexpectedly absent from that
/// batch (a data-integrity violation, unreachable in v1 - no company deletion)
/// is dropped from the page rather than failing the whole request: unlike
/// 2.2a's single-row detail read, a search page reads many rows, so one bad row
/// failing the page would be a worse regression for a case that cannot occur
/// (spec Design Notes).
/// </summary>
internal sealed class SearchJobPostingsHandler(JobPostingsDbContext db, IIdentityApi identityApi)
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    // Npgsql's two-argument EF.Functions.ILike(match, pattern) translates to
    // `... ILIKE pattern ESCAPE ''` - an explicit *empty* escape string, which
    // disables pattern-escaping entirely (unlike plain SQL ILIKE, whose
    // default escape character is backslash when no ESCAPE clause is given).
    // The three-argument overload must be used to pin the escape character
    // back to backslash, matching what EscapeLikePattern below produces.
    private const string LikeEscapeCharacter = "\\";

    public async Task<IResult> HandleAsync(
        string? query,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var clampedPage = page is null or < 1 ? 1 : page.Value;
        var clampedPageSize = pageSize is null or < 1 or > MaxPageSize ? DefaultPageSize : pageSize.Value;

        var pattern = $"%{EscapeLikePattern(query ?? string.Empty)}%";

        var matches = db.JobPostings
            .Where(p =>
                EF.Functions.ILike(p.Title, pattern, LikeEscapeCharacter) ||
                EF.Functions.ILike(p.Description, pattern, LikeEscapeCharacter));

        var total = await matches.CountAsync(cancellationToken);

        // long arithmetic, clamped to int.MaxValue: (page - 1) * pageSize can
        // overflow a 32-bit int for a very large `page` (~21,474,838+ at the
        // default pageSize) and wrap negative, which Postgres then rejects as
        // a negative OFFSET (500) instead of the matrix's "page beyond the
        // last page" empty-page behavior. Clamping to int.MaxValue instead
        // still skips past every real row, so the result is the same empty
        // page.
        var skip = (long)(clampedPage - 1) * clampedPageSize;
        var clampedSkip = skip > int.MaxValue ? int.MaxValue : (int)skip;

        var rows = await matches
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip(clampedSkip)
            .Take(clampedPageSize)
            .Select(p => new { p.Id, p.Title, p.Description, p.OwnerCompanyId })
            .ToListAsync(cancellationToken);

        var ownerIds = rows.Select(r => r.OwnerCompanyId).Distinct().ToArray();
        var companies = identityApi.GetCompanies(ownerIds);

        var items = new List<JobPostingSearchResultResponse>(rows.Count);
        foreach (var row in rows)
        {
            if (!companies.TryGetValue(row.OwnerCompanyId, out var company))
            {
                // Data-integrity violation, unreachable in v1 (no company
                // deletion): drop the row rather than fail the whole page
                // (spec Design Notes).
                continue;
            }

            items.Add(new JobPostingSearchResultResponse(
                row.Id.ToString(),
                row.Title,
                row.Description,
                company.DisplayName));
        }

        return Results.Ok(new Page<JobPostingSearchResultResponse>(items, clampedPage, clampedPageSize, total));
    }

    /// <summary>
    /// Escapes literal <c>%</c>, <c>_</c>, and <c>\</c> in <paramref name="keyword"/>
    /// so they match literally in an <c>ILIKE</c> pattern rather than as SQL
    /// wildcards (spec Intent / Design Notes) - required for "substring match" to
    /// hold for any keyword, not a policy choice.
    /// </summary>
    private static string EscapeLikePattern(string keyword) =>
        keyword
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}

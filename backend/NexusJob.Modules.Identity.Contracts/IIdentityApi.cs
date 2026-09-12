namespace NexusJob.Modules.Identity.Contracts;

/// <summary>
/// Identity's cross-module read surface (AD-1 / AD-6 / AD-19). A synchronous,
/// in-process service resolved from DI: another module calls it to obtain a
/// Company's display name without touching the <c>identity</c> schema (AD-5).
/// Exposes named DTOs only, never Identity entities.
/// </summary>
public interface IIdentityApi
{
    /// <summary>
    /// Returns the summary of the Company with <paramref name="id"/>, or
    /// <see langword="null"/> when no such Company exists. Use for a single-row
    /// lookup (a detail read); a list/table projection must use
    /// <see cref="GetCompanies"/> instead of calling this per row (AD-19).
    /// </summary>
    CompanySummaryDto? GetCompany(Guid id);

    /// <summary>
    /// Batch getter for list projections (AD-19): returns a map from Company id to
    /// its summary, containing an entry only for each id in <paramref name="ids"/>
    /// that resolves to a real Company. Unknown ids are absent from the map; an
    /// empty <paramref name="ids"/> yields an empty map with no database
    /// round-trip.
    /// </summary>
    IReadOnlyDictionary<Guid, CompanySummaryDto> GetCompanies(IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Returns the summary of the Job Seeker with <paramref name="id"/>, or
    /// <see langword="null"/> when no such Job Seeker exists. Use for a single-row
    /// lookup (a detail read); a list/table projection must use
    /// <see cref="GetJobSeekers"/> instead of calling this per row (AD-19).
    /// </summary>
    JobSeekerSummaryDto? GetJobSeeker(Guid id);

    /// <summary>
    /// Batch getter for list projections (AD-19): returns a map from Job Seeker id
    /// to its summary, containing an entry only for each id in
    /// <paramref name="ids"/> that resolves to a real Job Seeker. Unknown ids are
    /// absent from the map; an empty <paramref name="ids"/> yields an empty map
    /// with no database round-trip.
    /// </summary>
    IReadOnlyDictionary<Guid, JobSeekerSummaryDto> GetJobSeekers(IReadOnlyCollection<Guid> ids);
}

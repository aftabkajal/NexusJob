namespace NexusJob.Modules.JobPostings.Contracts;

/// <summary>
/// JobPostings' cross-module read surface (AD-1 / AD-6 / AD-19), published for
/// Epic 3 consumers (Applications). A synchronous, in-process service resolved
/// from DI; exposes named DTOs only, never JobPostings entities. No consumer
/// calls it yet - this story publishes it only.
/// </summary>
public interface IJobPostingsApi
{
    /// <summary>
    /// Returns the owning Company's account id for the posting with
    /// <paramref name="postingId"/>, or <see langword="null"/> when no such
    /// posting exists (AD-9 / AD-19).
    /// </summary>
    Guid? GetPostingOwner(Guid postingId);

    /// <summary>
    /// Batch getter for list projections (AD-19): returns a map from posting id to
    /// its summary, containing an entry only for each id in
    /// <paramref name="postingIds"/> that resolves to a real posting. Unknown ids
    /// are absent from the map; an empty <paramref name="postingIds"/>
    /// yields an empty map with no database round-trip.
    /// </summary>
    IReadOnlyDictionary<Guid, JobPostingSummaryDto> GetPostingSummaries(IReadOnlyCollection<Guid> postingIds);
}

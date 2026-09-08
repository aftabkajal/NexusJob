using NexusJob.Modules.JobPostings.Contracts;
using NexusJob.Modules.JobPostings.Persistence;

namespace NexusJob.Modules.JobPostings;

/// <summary>
/// In-process implementation of <see cref="IJobPostingsApi"/> over
/// <see cref="JobPostingsDbContext"/> (AD-6). Synchronous EF projection queries -
/// <c>JobPosting</c> rows are small. Takes no <see cref="IIdentityApi"/>
/// dependency: <see cref="JobPostingSummaryDto"/> carries only posting-owned
/// fields, and a consumer that needs the Company name composes it via
/// <c>IIdentityApi.GetCompanies</c> (AD-19). Registered
/// <c>AddScoped&lt;IJobPostingsApi, JobPostingsApi&gt;()</c> in
/// <see cref="JobPostingsModule.AddJobPostingsModule"/>.
/// </summary>
internal sealed class JobPostingsApi(JobPostingsDbContext db) : IJobPostingsApi
{
    public Guid? GetPostingOwner(Guid postingId) =>
        db.JobPostings
            .Where(p => p.Id == postingId)
            .Select(p => (Guid?)p.OwnerCompanyId)
            .SingleOrDefault();

    public IReadOnlyDictionary<Guid, JobPostingSummaryDto> GetPostingSummaries(IReadOnlyCollection<Guid> postingIds)
    {
        if (postingIds.Count == 0)
        {
            return new Dictionary<Guid, JobPostingSummaryDto>();
        }

        return db.JobPostings
            .Where(p => postingIds.Contains(p.Id))
            .Select(p => new JobPostingSummaryDto(p.Id, p.Title, p.Description, p.OwnerCompanyId, p.CreatedAt))
            .ToDictionary(x => x.Id);
    }
}

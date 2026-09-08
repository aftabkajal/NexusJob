using NexusJob.Modules.Identity.Contracts;
using NexusJob.Modules.Identity.Persistence;

namespace NexusJob.Modules.Identity;

/// <summary>
/// In-process implementation of <see cref="IIdentityApi"/> over
/// <see cref="IdentityDbContext"/> (AD-6). Synchronous EF projection queries -
/// <c>CompanyAccount</c> rows are tiny - mirroring
/// <see cref="Features.GetMe.GetMeHandler"/>'s <c>DisplayName</c> projection.
/// Registered <c>AddScoped&lt;IIdentityApi, IdentityApi&gt;()</c> in
/// <see cref="IdentityModule.AddIdentityModule"/>.
/// </summary>
internal sealed class IdentityApi(IdentityDbContext db) : IIdentityApi
{
    public CompanySummaryDto? GetCompany(Guid id) =>
        db.CompanyAccounts
            .Where(a => a.Id == id)
            .Select(a => new CompanySummaryDto(a.Id, a.DisplayName))
            .SingleOrDefault();

    public IReadOnlyDictionary<Guid, CompanySummaryDto> GetCompanies(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, CompanySummaryDto>();
        }

        return db.CompanyAccounts
            .Where(a => ids.Contains(a.Id))
            .Select(a => new CompanySummaryDto(a.Id, a.DisplayName))
            .ToDictionary(x => x.Id);
    }
}

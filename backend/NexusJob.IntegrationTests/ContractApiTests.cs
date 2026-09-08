using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NexusJob.Modules.Identity.Contracts;
using NexusJob.Modules.JobPostings.Contracts;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// Pins the published cross-module Contracts (spec 2.2a / AD-19): resolves
/// <see cref="IIdentityApi"/> and <see cref="IJobPostingsApi"/> from a DI scope
/// off the hosted app and exercises every getter - hit, miss, and empty batch.
/// This is the sole coverage for the batch getters and for all of
/// <see cref="IJobPostingsApi"/> (nothing consumes it yet).
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class ContractApiTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private static string NewEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    /// <summary>Registers a Company and creates one posting; returns both ids and the registered display name.</summary>
    private async Task<(Guid CompanyId, Guid PostingId, string DisplayName)> SeedCompanyAndPostingAsync(string displayName)
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync(displayName, NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();

        using var created = await client.CreatePostingAsync($"{displayName} role", $"Posted by {displayName}.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var posting = await created.Content.ReadFromJsonAsync<JobPostingDto>();

        return (Guid.Parse(account!.Id), Guid.Parse(posting!.Id), displayName);
    }

    // ---- IIdentityApi.GetCompany - hit / miss ---------------------------

    [Fact]
    public async Task IdentityApi_GetCompany_returns_the_summary_for_a_known_id_and_null_for_an_unknown_one()
    {
        var (companyId, _, displayName) = await SeedCompanyAndPostingAsync("Acme Inc.");

        using var scope = fixture.Services.CreateScope();
        var identityApi = scope.ServiceProvider.GetRequiredService<IIdentityApi>();

        var hit = identityApi.GetCompany(companyId);
        Assert.NotNull(hit);
        Assert.Equal(companyId, hit!.Id);
        Assert.Equal(displayName, hit.DisplayName);

        Assert.Null(identityApi.GetCompany(Guid.NewGuid()));
    }

    // ---- IIdentityApi.GetCompanies - batch: known-only, empty ----------

    [Fact]
    public async Task IdentityApi_GetCompanies_maps_only_the_known_ids_and_handles_an_empty_input()
    {
        var (companyA, _, nameA) = await SeedCompanyAndPostingAsync("Globex Corp.");
        var (companyB, _, nameB) = await SeedCompanyAndPostingAsync("Initech LLC");
        var unknown = Guid.NewGuid();

        using var scope = fixture.Services.CreateScope();
        var identityApi = scope.ServiceProvider.GetRequiredService<IIdentityApi>();

        var map = identityApi.GetCompanies([companyA, companyB, unknown]);

        Assert.Equal(2, map.Count);
        Assert.Equal(nameA, map[companyA].DisplayName);
        Assert.Equal(nameB, map[companyB].DisplayName);
        Assert.False(map.ContainsKey(unknown));
        Assert.All(map, pair => Assert.Equal(pair.Key, pair.Value.Id));

        Assert.Empty(identityApi.GetCompanies([]));
    }

    // ---- IJobPostingsApi.GetPostingOwner - hit / miss -----------------

    [Fact]
    public async Task JobPostingsApi_GetPostingOwner_returns_the_owner_for_a_known_posting_and_null_otherwise()
    {
        var (companyId, postingId, _) = await SeedCompanyAndPostingAsync("Umbrella Co.");

        using var scope = fixture.Services.CreateScope();
        var jobPostingsApi = scope.ServiceProvider.GetRequiredService<IJobPostingsApi>();

        Assert.Equal(companyId, jobPostingsApi.GetPostingOwner(postingId));
        Assert.Null(jobPostingsApi.GetPostingOwner(Guid.NewGuid()));
    }

    // ---- IJobPostingsApi.GetPostingSummaries - batch: known-only, empty --

    [Fact]
    public async Task JobPostingsApi_GetPostingSummaries_maps_only_the_known_posting_ids_and_handles_an_empty_input()
    {
        var (companyA, postingA, _) = await SeedCompanyAndPostingAsync("Stark Industries");
        var (_, postingB, _) = await SeedCompanyAndPostingAsync("Wayne Enterprises");
        var unknown = Guid.NewGuid();

        using var scope = fixture.Services.CreateScope();
        var jobPostingsApi = scope.ServiceProvider.GetRequiredService<IJobPostingsApi>();

        var map = jobPostingsApi.GetPostingSummaries([postingA, postingB, unknown]);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(postingA));
        Assert.True(map.ContainsKey(postingB));
        Assert.False(map.ContainsKey(unknown));

        var summaryA = map[postingA];
        Assert.Equal(postingA, summaryA.Id);
        Assert.Equal(companyA, summaryA.OwnerCompanyId);
        Assert.Equal("Stark Industries role", summaryA.Title);
        Assert.Equal("Posted by Stark Industries.", summaryA.Description);
        Assert.NotEqual(default, summaryA.CreatedAt);
        Assert.All(map, pair => Assert.Equal(pair.Key, pair.Value.Id));

        Assert.Empty(jobPostingsApi.GetPostingSummaries([]));
    }
}

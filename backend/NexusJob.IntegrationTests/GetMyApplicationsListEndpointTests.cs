using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 3.3a's I/O &amp; Edge-Case Matrix for
/// <c>GET /api/applications/mine/list</c>, run against a real Postgres
/// (Testcontainers) through the shared <see cref="IdentityApiFixture"/>. Each
/// test creates its own fresh Job Seeker (<see cref="NewJobSeekerAsync"/>), so
/// - unlike the shared-fixture postings/search suites - a test's <c>total</c>
/// assertions are exact: no other test's rows can land under this seeker's id.
///
/// Rows are inserted directly via <see cref="ApplicationsDatabase.InsertApplicationAsync"/>
/// with an explicit <c>submittedAt</c> so ordering and pagination are
/// deterministic, rather than relying on real-time clock ordering from
/// back-to-back <c>POST /api/applications</c> calls. Postings referenced by a
/// non-orphan row are still real, API-created postings, so title resolution
/// through <c>IJobPostingsApi.GetPostingSummaries</c> is exercised for real.
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class GetMyApplicationsListEndpointTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private ApplicationsDatabase Db => new(fixture.ConnectionString);

    private static string NewEmail() => $"u-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    /// <summary>Registers a Company and creates one posting; returns the posting id and its title.</summary>
    private async Task<(Guid PostingId, string Title)> NewPostingAsync(string title)
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync($"Company {Guid.NewGuid():N}", NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        using var created = await client.CreatePostingAsync(title, "A role worth applying to.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var posting = await created.Content.ReadFromJsonAsync<JobPostingDto>();

        return (Guid.Parse(posting!.Id), title);
    }

    /// <summary>Registers a Job Seeker; returns its client (cookie set) and account id.</summary>
    private async Task<(AuthApiClient Client, Guid SeekerId)> NewJobSeekerAsync()
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync("Dana Scully", NewEmail(), Password, accountType: "job_seeker");
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();
        return (client, Guid.Parse(account!.Id));
    }

    private async Task<PageDto<MyApplicationListItemDto>> ListAsync(AuthApiClient client, int? page = null, int? pageSize = null)
    {
        using var response = await client.GetMyApplicationsAsync(page, pageSize);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<PageDto<MyApplicationListItemDto>>();
        Assert.NotNull(body);
        return body!;
    }

    // ---- Row: Applied to several postings -----------------------------

    [Fact]
    public async Task Applied_to_several_postings_returns_them_most_recent_first_with_resolved_titles()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var (postingA, titleA) = await NewPostingAsync($"Backend Engineer {Guid.NewGuid():N}");
        var (postingB, titleB) = await NewPostingAsync($"Frontend Engineer {Guid.NewGuid():N}");

        var now = DateTimeOffset.UtcNow;
        var olderId = await Db.InsertApplicationAsync(postingA, seekerId, now.AddMinutes(-10));
        var newerId = await Db.InsertApplicationAsync(postingB, seekerId, now);

        var page = await ListAsync(seeker, page: 1);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(newerId.ToString(), page.Items[0].ApplicationId);
        Assert.Equal(postingB.ToString(), page.Items[0].JobPostingId);
        Assert.Equal(titleB, page.Items[0].JobPostingTitle);
        Assert.Equal(olderId.ToString(), page.Items[1].ApplicationId);
        Assert.Equal(postingA.ToString(), page.Items[1].JobPostingId);
        Assert.Equal(titleA, page.Items[1].JobPostingTitle);
    }

    // ---- Row: No applications -------------------------------------

    [Fact]
    public async Task No_applications_returns_an_empty_page_with_the_default_shape()
    {
        var (seeker, _) = await NewJobSeekerAsync();

        var page = await ListAsync(seeker);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.Total);
    }

    // ---- Row: Pagination --------------------------------------------

    [Fact]
    public async Task Requesting_page_2_returns_the_next_pages_rows_offset_by_page_size()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var (postingA, titleA) = await NewPostingAsync($"Role A {Guid.NewGuid():N}");
        var (postingB, titleB) = await NewPostingAsync($"Role B {Guid.NewGuid():N}");
        var (postingC, titleC) = await NewPostingAsync($"Role C {Guid.NewGuid():N}");

        var now = DateTimeOffset.UtcNow;
        await Db.InsertApplicationAsync(postingA, seekerId, now.AddMinutes(-20)); // oldest
        await Db.InsertApplicationAsync(postingB, seekerId, now.AddMinutes(-10)); // middle
        await Db.InsertApplicationAsync(postingC, seekerId, now); // newest

        var firstPage = await ListAsync(seeker, page: 1, pageSize: 2);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, firstPage.Total);
        Assert.Equal(titleC, firstPage.Items[0].JobPostingTitle);
        Assert.Equal(titleB, firstPage.Items[1].JobPostingTitle);

        var secondPage = await ListAsync(seeker, page: 2, pageSize: 2);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(3, secondPage.Total);
        Assert.Equal(titleA, secondPage.Items[0].JobPostingTitle);
    }

    // ---- Row: page/pageSize out of range is clamped ------------------

    [Fact]
    public async Task A_page_number_below_1_is_clamped_to_1()
    {
        var (seeker, _) = await NewJobSeekerAsync();

        var page = await ListAsync(seeker, page: 0);

        Assert.Equal(1, page.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    public async Task Out_of_range_page_size_is_clamped_to_the_default(int requestedPageSize)
    {
        var (seeker, _) = await NewJobSeekerAsync();

        var page = await ListAsync(seeker, pageSize: requestedPageSize);

        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task The_maximum_page_size_of_100_passes_through_unclamped()
    {
        // Pins the clamp's inclusive upper bound (`> MaxPageSize`, not
        // `>= MaxPageSize`): 100 itself must NOT fall back to the default 20.
        var (seeker, _) = await NewJobSeekerAsync();

        var page = await ListAsync(seeker, pageSize: 100);

        Assert.Equal(100, page.PageSize);
    }

    // ---- Row: Page beyond the last page ------------------------------

    [Fact]
    public async Task Requesting_a_page_beyond_the_last_page_returns_an_empty_page_but_echoes_the_true_total()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var (posting, _) = await NewPostingAsync($"Role {Guid.NewGuid():N}");
        await Db.InsertApplicationAsync(posting, seekerId, DateTimeOffset.UtcNow);

        var page = await ListAsync(seeker, page: 999);

        Assert.Empty(page.Items);
        Assert.Equal(999, page.Page);
        Assert.Equal(1, page.Total);
    }

    // ---- Row: Orphan posting (unreachable in v1) --------------------

    [Fact]
    public async Task An_application_whose_posting_cannot_be_resolved_is_dropped_but_still_counted_in_total()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var (realPosting, realTitle) = await NewPostingAsync($"Real role {Guid.NewGuid():N}");

        var now = DateTimeOffset.UtcNow;
        await Db.InsertApplicationAsync(realPosting, seekerId, now);

        // An orphan application: job_posting_id matches no real posting. Only
        // reachable by a direct insert - every API-created application targets
        // a posting whose existence was checked at apply time (unreachable in
        // v1: nothing deletes a posting).
        var orphanPostingId = Guid.NewGuid();
        await Db.InsertApplicationAsync(orphanPostingId, seekerId, now.AddMinutes(-5));

        var page = await ListAsync(seeker);

        // The DB match count includes the orphan row; the projected page drops it.
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(realPosting.ToString(), page.Items[0].JobPostingId);
        Assert.Equal(realTitle, page.Items[0].JobPostingTitle);
    }

    // ---- Row: Anonymous / Company session ----------------------------

    [Fact]
    public async Task Anonymous_caller_returns_401_problem_details()
    {
        using var response = await NewClient().GetMyApplicationsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Company_session_returns_403_problem_details()
    {
        var company = NewClient();
        using (var registration = await company.RegisterAsync("Initrode LLC", NewEmail(), Password))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        using var response = await company.GetMyApplicationsAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.NotNull(problem);
        Assert.Equal((int)response.StatusCode, problem!.Status);
    }

    private sealed record ProblemBody
    {
        public string? Title { get; init; }

        public string? Detail { get; init; }

        public int? Status { get; init; }
    }
}

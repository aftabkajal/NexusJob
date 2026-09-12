using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 3.4a's I/O &amp; Edge-Case Matrix for
/// <c>GET /api/job-postings/mine</c>, run against a real Postgres
/// (Testcontainers) through the shared <see cref="IdentityApiFixture"/>. Each
/// test registers its own fresh Company (<see cref="NewCompanyAsync"/>), so -
/// unlike the shared-fixture search suite - a test's <c>total</c> assertions
/// are exact: no other test's postings can land under this Company's id.
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class GetMyJobPostingsEndpointTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private static string NewEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    /// <summary>Registers a Company; returns its client (cookie set) and account id.</summary>
    private async Task<(AuthApiClient Client, Guid CompanyId)> NewCompanyAsync(string displayName)
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync(displayName, NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();
        return (client, Guid.Parse(account!.Id));
    }

    private static async Task<Guid> ReadPostingIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JobPostingDto>();
        return Guid.Parse(body!.Id);
    }

    private async Task<PageDto<JobPostingMineItemDto>> ListAsync(AuthApiClient client, int? page = null, int? pageSize = null)
    {
        using var response = await client.GetMyPostingsAsync(page, pageSize);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<PageDto<JobPostingMineItemDto>>();
        Assert.NotNull(body);
        return body!;
    }

    // ---- Row: My postings, has some ------------------------------------

    [Fact]
    public async Task Company_with_postings_from_itself_and_another_company_sees_only_its_own_most_recent_first()
    {
        var (owner, _) = await NewCompanyAsync("Acme Inc.");
        var (other, _) = await NewCompanyAsync("Globex Corp.");

        using (var created = await other.CreatePostingAsync("Other's role", "Not mine."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using var olderResponse = await owner.CreatePostingAsync("Older role", "First posted.");
        Assert.Equal(HttpStatusCode.OK, olderResponse.StatusCode);
        var olderId = await ReadPostingIdAsync(olderResponse);

        using var newerResponse = await owner.CreatePostingAsync("Newer role", "Second posted.");
        Assert.Equal(HttpStatusCode.OK, newerResponse.StatusCode);
        var newerId = await ReadPostingIdAsync(newerResponse);

        var page = await ListAsync(owner, page: 1);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(newerId.ToString(), page.Items[0].Id);
        Assert.Equal(olderId.ToString(), page.Items[1].Id);
    }

    // ---- Row: My postings, none -----------------------------------------

    [Fact]
    public async Task Company_with_no_postings_returns_an_empty_page_with_the_default_shape()
    {
        var (owner, _) = await NewCompanyAsync("Initech LLC");

        var page = await ListAsync(owner);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.Total);
    }

    // ---- Row: My postings clamping ---------------------------------------

    [Fact]
    public async Task A_page_number_below_1_is_clamped_to_1()
    {
        var (owner, _) = await NewCompanyAsync("Umbrella Co.");

        var page = await ListAsync(owner, page: 0);

        Assert.Equal(1, page.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)] // pins the exact one-past-max boundary, not just a far-out value
    [InlineData(500)]
    public async Task Out_of_range_page_size_is_clamped_to_the_default(int requestedPageSize)
    {
        var (owner, _) = await NewCompanyAsync("Stark Industries");

        var page = await ListAsync(owner, pageSize: requestedPageSize);

        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task The_maximum_page_size_of_100_passes_through_unclamped()
    {
        // Pins the clamp's inclusive upper bound (`> MaxPageSize`, not
        // `>= MaxPageSize`): 100 itself must NOT fall back to the default 20.
        var (owner, _) = await NewCompanyAsync("Wayne Enterprises");

        var page = await ListAsync(owner, pageSize: 100);

        Assert.Equal(100, page.PageSize);
    }

    // ---- Pagination -------------------------------------------------------

    [Fact]
    public async Task Requesting_page_2_returns_the_next_pages_rows_offset_by_page_size()
    {
        var (owner, _) = await NewCompanyAsync("Aperture Science");

        using (var created = await owner.CreatePostingAsync("Role A", "Oldest."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using (var created = await owner.CreatePostingAsync("Role B", "Middle."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using var newest = await owner.CreatePostingAsync("Role C", "Newest.");
        Assert.Equal(HttpStatusCode.OK, newest.StatusCode);
        var newestId = await ReadPostingIdAsync(newest);

        var firstPage = await ListAsync(owner, page: 1, pageSize: 2);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, firstPage.Total);
        Assert.Equal(newestId.ToString(), firstPage.Items[0].Id);

        var secondPage = await ListAsync(owner, page: 2, pageSize: 2);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(3, secondPage.Total);
        Assert.Equal("Role A", secondPage.Items[0].Title);
    }

    // ---- Row: page beyond the last page ------------------------------------

    [Fact]
    public async Task Requesting_a_page_beyond_the_last_page_returns_an_empty_page_but_echoes_the_true_total()
    {
        var (owner, _) = await NewCompanyAsync("Black Mesa");
        using (var created = await owner.CreatePostingAsync("Role", "Only posting."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        var page = await ListAsync(owner, page: 999);

        Assert.Empty(page.Items);
        Assert.Equal(999, page.Page);
        Assert.Equal(1, page.Total);
    }

    // ---- Row: My postings, anonymous / Job Seeker --------------------------

    [Fact]
    public async Task Anonymous_caller_returns_401_problem_details()
    {
        using var response = await NewClient().GetMyPostingsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Job_seeker_session_returns_403_problem_details()
    {
        var jobSeeker = NewClient();
        using (var registration = await jobSeeker.RegisterAsync("Fox Mulder", NewEmail(), Password, accountType: "job_seeker"))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        using var response = await jobSeeker.GetMyPostingsAsync();

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

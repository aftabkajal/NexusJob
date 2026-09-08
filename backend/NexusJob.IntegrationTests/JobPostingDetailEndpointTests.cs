using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 2.2a's I/O &amp; Edge-Case Matrix for
/// <c>GET /api/job-postings/{id}</c>, run against a real Postgres (Testcontainers)
/// through the shared <see cref="IdentityApiFixture"/>. The endpoint is anonymous
/// (AD-18): the owning Company's display name is resolved through
/// <c>IIdentityApi</c>, never a query against the <c>identity</c> schema.
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class JobPostingDetailEndpointTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private static string NewEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    /// <summary>Registers a Company as <paramref name="displayName"/> and creates one posting; returns the created id.</summary>
    private async Task<Guid> CreatePostingAsync(string displayName, string title, string description)
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync(displayName, NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        using var created = await client.CreatePostingAsync(title, description);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JobPostingDto>();
        return Guid.Parse(body!.Id);
    }

    // ---- Row: Read an existing posting, anonymous -------------------------

    [Fact]
    public async Task Get_posting_by_id_without_a_session_returns_200_with_the_detail_representation()
    {
        var id = await CreatePostingAsync("Acme Inc.", "Senior .NET Engineer", "Build the NexusJob core loop.");

        // A brand-new client: no auth cookie at all.
        var anonymous = NewClient();
        using var response = await anonymous.GetPostingAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // No envelope: the body is exactly { id, title, description, companyName }.
        using (var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var names = payload.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(["companyName", "description", "id", "title"], names);
        }

        var body = await response.Content.ReadFromJsonAsync<JobPostingDetailDto>();
        Assert.NotNull(body);
        Assert.Equal(id.ToString(), body!.Id);
        Assert.Equal("Senior .NET Engineer", body.Title);
        Assert.Equal("Build the NexusJob core loop.", body.Description);
        Assert.Equal("Acme Inc.", body.CompanyName);
    }

    // ---- Row: Read an existing posting, authenticated -------------------

    [Fact]
    public async Task Get_posting_by_id_with_a_job_seeker_session_returns_the_identical_200()
    {
        var id = await CreatePostingAsync("Globex Corp.", "Data Engineer", "Own the pipeline.");

        // A signed-in Job Seeker: the endpoint is anonymous, so the session is ignored.
        var seeker = NewClient();
        using (var registration = await seeker.RegisterAsync("Dana Scully", NewEmail(), Password, accountType: "job_seeker"))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        using var response = await seeker.GetPostingAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobPostingDetailDto>();
        Assert.NotNull(body);
        Assert.Equal(id.ToString(), body!.Id);
        Assert.Equal("Data Engineer", body.Title);
        Assert.Equal("Own the pipeline.", body.Description);
        Assert.Equal("Globex Corp.", body.CompanyName);
    }

    // ---- Row: Well-formed id, no such posting --------------------------

    [Fact]
    public async Task Get_posting_by_id_for_an_unknown_guid_returns_404_problem_details()
    {
        var response = await NewClient().GetPostingAsync(Guid.NewGuid());

        using (response)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
            Assert.NotNull(problem);
            Assert.Equal(404, problem!.Status);
        }
    }

    // ---- Row: Non-GUID id segment -----------------------------------

    [Fact]
    public async Task Get_posting_by_id_with_a_non_guid_segment_returns_404_from_routing()
    {
        using var response = await NewClient().GetPostingRawAsync("not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The {id:guid} route constraint rejects the segment before the handler
        // runs, so this is a bare routing/fallback 404 - never the handler's
        // Results.Problem (which would be application/problem+json).
        Assert.NotEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---- Row: Owner Company cannot be resolved -> 500 ProblemDetails ----

    [Fact]
    public async Task Get_posting_by_id_whose_owner_company_is_missing_returns_500_problem_details()
    {
        // An orphan posting: its owner_company_id has no company_account. Only
        // reachable by a direct insert - every API-created posting's owner is a
        // real authenticated Company.
        var db = new JobPostingsDatabase(fixture.ConnectionString);
        var orphanId = await db.InsertPostingAsync(
            Guid.NewGuid(), "Ghost Role", "Nobody owns this posting.");

        using var response = await NewClient().GetPostingAsync(orphanId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem!.Status);
    }

    // ---- Row: Structured logs for a detail read carry no content -------

    [Fact]
    public async Task No_title_description_or_cookie_appears_in_the_structured_logs_for_a_detail_read()
    {
        const string title = "Confidential Widget Wrangler";
        const string description = "Secret sauce recipe maintenance.";
        var id = await CreatePostingAsync("Initech LLC", title, description);

        using (var response = await NewClient().GetPostingAsync(id))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var snapshot = fixture.Logs.Snapshot();

        // Guard against a vacuous pass: the detail read must have produced a log line.
        Assert.Contains(snapshot, line => line.Contains("/api/job-postings/", StringComparison.Ordinal));

        foreach (var line in snapshot)
        {
            Assert.DoesNotContain(title, line, StringComparison.Ordinal);
            Assert.DoesNotContain(description, line, StringComparison.Ordinal);
            Assert.DoesNotContain("Set-Cookie", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("X-CSRF-TOKEN", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record ProblemBody
    {
        public string? Title { get; init; }

        public int? Status { get; init; }
    }
}

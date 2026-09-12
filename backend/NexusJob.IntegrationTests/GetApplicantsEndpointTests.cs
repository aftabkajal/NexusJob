using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 3.4a's I/O &amp; Edge-Case Matrix for
/// <c>GET /api/applications?jobPostingId=</c>, run against a real Postgres
/// (Testcontainers) through the shared <see cref="IdentityApiFixture"/>. Each
/// test registers its own fresh Company and posting(s)
/// (<see cref="NewCompanyWithPostingAsync"/>), so a test's <c>total</c>
/// assertions are exact.
///
/// Rows are inserted directly via <see cref="ApplicationsDatabase.InsertApplicationAsync"/>
/// with an explicit <c>submittedAt</c> so ordering and pagination are
/// deterministic, mirroring <see cref="GetMyApplicationsListEndpointTests"/>.
/// Job Seekers referenced by a non-orphan row are still real, API-registered
/// accounts, so name/email resolution through <c>IIdentityApi.GetJobSeekers</c>
/// is exercised for real.
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class GetApplicantsEndpointTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private ApplicationsDatabase Db => new(fixture.ConnectionString);

    private static string NewEmail() => $"u-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    /// <summary>Registers a Company and creates one posting; returns the Company's client, its id, and the posting id.</summary>
    private async Task<(AuthApiClient Client, Guid CompanyId, Guid PostingId)> NewCompanyWithPostingAsync(string displayName)
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync(displayName, NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();

        using var created = await client.CreatePostingAsync($"{displayName} role", "A role worth applying to.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var posting = await created.Content.ReadFromJsonAsync<JobPostingDto>();

        return (client, Guid.Parse(account!.Id), Guid.Parse(posting!.Id));
    }

    /// <summary>Registers a Job Seeker; returns its account id, full name, and email.</summary>
    private async Task<(Guid Id, string FullName, string Email)> NewJobSeekerAsync(string fullName)
    {
        var email = NewEmail();
        var client = NewClient();
        using var registration = await client.RegisterAsync(fullName, email, Password, accountType: "job_seeker");
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();
        return (Guid.Parse(account!.Id), fullName, email);
    }

    private async Task<PageDto<ApplicantListItemDto>> ListAsync(
        AuthApiClient client, string jobPostingId, int? page = null, int? pageSize = null)
    {
        using var response = await client.GetApplicantsAsync(jobPostingId, page, pageSize);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<PageDto<ApplicantListItemDto>>();
        Assert.NotNull(body);
        return body!;
    }

    // ---- Row: Applicants, has some -------------------------------------

    [Fact]
    public async Task Applicants_from_two_different_accounts_are_returned_most_recent_first_with_resolved_identity()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Acme Inc.");
        var (seekerA, nameA, emailA) = await NewJobSeekerAsync("Fox Mulder");
        var (seekerB, nameB, emailB) = await NewJobSeekerAsync("Dana Scully");

        var now = DateTimeOffset.UtcNow;
        await Db.InsertApplicationAsync(postingId, seekerA, now.AddMinutes(-10));
        await Db.InsertApplicationAsync(postingId, seekerB, now);

        var page = await ListAsync(owner, postingId.ToString());

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(seekerB.ToString(), page.Items[0].JobSeekerId);
        Assert.Equal(nameB, page.Items[0].FullName);
        Assert.Equal(emailB, page.Items[0].Email);
        Assert.Equal(seekerA.ToString(), page.Items[1].JobSeekerId);
        Assert.Equal(nameA, page.Items[1].FullName);
        Assert.Equal(emailA, page.Items[1].Email);
    }

    // ---- Row: Applicants, none -------------------------------------------

    [Fact]
    public async Task Owned_posting_with_no_applicants_returns_an_empty_page_with_the_default_shape()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Globex Corp.");

        var page = await ListAsync(owner, postingId.ToString());

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.Total);
    }

    // ---- Row: Applicants, missing posting / another Company's posting -----
    // The byte-identical-404 assertion (AD-9): missing and not-owned must
    // produce the exact same JSON body.

    [Fact]
    public async Task Missing_posting_and_another_companys_posting_both_yield_the_same_404_body()
    {
        var (owner, _, _) = await NewCompanyWithPostingAsync("Initech LLC");
        var (_, _, otherPostingId) = await NewCompanyWithPostingAsync("Umbrella Co.");

        using var missingResponse = await owner.GetApplicantsAsync(Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("application/problem+json", missingResponse.Content.Headers.ContentType?.MediaType);

        using var notOwnedResponse = await owner.GetApplicantsAsync(otherPostingId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, notOwnedResponse.StatusCode);
        Assert.Equal("application/problem+json", notOwnedResponse.Content.Headers.ContentType?.MediaType);

        // Full-body comparison (every property the framework writes),
        // excluding only "traceId" - the one field the default
        // ProblemDetails writer stamps per-request (from
        // Activity.Current/HttpContext.TraceIdentifier) even with no
        // CustomizeProblemDetails callback configured (confirmed empirically:
        // the two raw bodies differ in nothing else). Comparing the full
        // document this way - rather than a handful of named fields on a
        // typed DTO - would catch a divergence in any property, known or not.
        var missingBody = await missingResponse.Content.ReadAsStringAsync();
        var notOwnedBody = await notOwnedResponse.Content.ReadAsStringAsync();
        Assert.Equal(WithoutTraceId(notOwnedBody), WithoutTraceId(missingBody));

        var missing = JsonSerializer.Deserialize<ProblemBody>(
            missingBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(404, missing!.Status);
    }

    // ---- Row: Applicants, malformed/missing id -----------------------------

    [Fact]
    public async Task A_non_guid_job_posting_id_returns_400_validation_problem_naming_the_field()
    {
        var (owner, _, _) = await NewCompanyWithPostingAsync("Stark Industries");

        using var response = await owner.GetApplicantsAsync("not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains("jobPostingId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Omitted_job_posting_id_returns_400_validation_problem_naming_the_field()
    {
        var (owner, _, _) = await NewCompanyWithPostingAsync("Wayne Enterprises");

        using var response = await owner.GetApplicantsAsync(null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains("jobPostingId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Blank_job_posting_id_returns_400_validation_problem_naming_the_field()
    {
        // Distinct wire case from "omitted": the query parameter is present
        // but binds to "" rather than null.
        var (owner, _, _) = await NewCompanyWithPostingAsync("LexCorp");

        using var response = await owner.GetApplicantsAsync("");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains("jobPostingId", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Row: Applicants clamping -------------------------------------------

    [Fact]
    public async Task A_page_number_below_1_is_clamped_to_1()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Aperture Science");

        var page = await ListAsync(owner, postingId.ToString(), page: 0);

        Assert.Equal(1, page.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)] // pins the exact one-past-max boundary, not just a far-out value
    [InlineData(500)]
    public async Task Out_of_range_page_size_is_clamped_to_the_default(int requestedPageSize)
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Black Mesa");

        var page = await ListAsync(owner, postingId.ToString(), pageSize: requestedPageSize);

        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task The_maximum_page_size_of_100_passes_through_unclamped()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Wonka Industries");

        var page = await ListAsync(owner, postingId.ToString(), pageSize: 100);

        Assert.Equal(100, page.PageSize);
    }

    // ---- Pagination -----------------------------------------------------

    [Fact]
    public async Task Requesting_page_2_returns_the_next_pages_rows_offset_by_page_size()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Tyrell Corp.");
        var (seekerA, _, _) = await NewJobSeekerAsync("Applicant A");
        var (seekerB, _, _) = await NewJobSeekerAsync("Applicant B");
        var (seekerC, _, _) = await NewJobSeekerAsync("Applicant C");

        var now = DateTimeOffset.UtcNow;
        await Db.InsertApplicationAsync(postingId, seekerA, now.AddMinutes(-20)); // oldest
        await Db.InsertApplicationAsync(postingId, seekerB, now.AddMinutes(-10)); // middle
        var newestId = await Db.InsertApplicationAsync(postingId, seekerC, now); // newest
        _ = newestId;

        var firstPage = await ListAsync(owner, postingId.ToString(), page: 1, pageSize: 2);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, firstPage.Total);
        Assert.Equal(seekerC.ToString(), firstPage.Items[0].JobSeekerId);
        Assert.Equal(seekerB.ToString(), firstPage.Items[1].JobSeekerId);

        var secondPage = await ListAsync(owner, postingId.ToString(), page: 2, pageSize: 2);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(3, secondPage.Total);
        Assert.Equal(seekerA.ToString(), secondPage.Items[0].JobSeekerId);
    }

    // ---- Row: Applicants, anonymous / Job Seeker -----------------------

    [Fact]
    public async Task Anonymous_caller_returns_401_problem_details()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Cyberdyne Systems");

        using var response = await NewClient().GetApplicantsAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Job_seeker_session_returns_403_problem_details()
    {
        var (_, _, postingId) = await NewCompanyWithPostingAsync("Massive Dynamic");
        var jobSeeker = NewClient();
        using (var registration = await jobSeeker.RegisterAsync("Fox Mulder", NewEmail(), Password, accountType: "job_seeker"))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        using var response = await jobSeeker.GetApplicantsAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    // ---- Row: Applicants, orphan applicant (unreachable in v1) ----------

    [Fact]
    public async Task An_application_whose_job_seeker_cannot_be_resolved_is_dropped_but_still_counted_in_total()
    {
        var (owner, _, postingId) = await NewCompanyWithPostingAsync("Soylent Corp.");
        var (realSeeker, realName, realEmail) = await NewJobSeekerAsync("Real Applicant");

        var now = DateTimeOffset.UtcNow;
        await Db.InsertApplicationAsync(postingId, realSeeker, now);

        // An orphan application: job_seeker_id matches no real account. Only
        // reachable by a direct insert - every API-created application's
        // applicant is a real authenticated Job Seeker (unreachable in v1: no
        // Job Seeker deletion).
        var orphanSeekerId = Guid.NewGuid();
        await Db.InsertApplicationAsync(postingId, orphanSeekerId, now.AddMinutes(-5));

        var page = await ListAsync(owner, postingId.ToString());

        // The DB match count includes the orphan row; the projected page drops it.
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(realSeeker.ToString(), page.Items[0].JobSeekerId);
        Assert.Equal(realName, page.Items[0].FullName);
        Assert.Equal(realEmail, page.Items[0].Email);
    }

    /// <summary>
    /// Re-serializes a ProblemDetails JSON body with the "traceId" extension
    /// removed and its remaining properties in a fixed (alphabetical) order,
    /// so two bodies that differ only in that per-request field compare equal.
    /// </summary>
    private static string WithoutTraceId(string problemDetailsJson)
    {
        using var document = JsonDocument.Parse(problemDetailsJson);
        var properties = document.RootElement.EnumerateObject()
            .Where(p => p.Name != "traceId")
            .OrderBy(p => p.Name, StringComparer.Ordinal);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in properties)
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
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
        public string? Type { get; init; }

        public string? Title { get; init; }

        public string? Detail { get; init; }

        public int? Status { get; init; }
    }

    private sealed record ValidationProblemBody
    {
        public IDictionary<string, string[]>? Errors { get; init; }
    }
}

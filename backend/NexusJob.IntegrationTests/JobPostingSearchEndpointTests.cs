using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 2.3a's I/O &amp; Edge-Case Matrix for
/// <c>GET /api/job-postings</c>, run against a real Postgres (Testcontainers)
/// through the shared <see cref="IdentityApiFixture"/>. The fixture's Postgres
/// container - and therefore <c>job_postings.job_posting</c> - is shared across
/// every test class in <see cref="IdentityApiCollection"/>, so rows from other
/// tests are already present by the time these run. Every test therefore scopes
/// its assertions to postings it creates itself, usually by searching for a
/// fresh <see cref="Guid"/>-derived marker embedded in a title/description that
/// no other test can coincidentally produce, rather than asserting an exact
/// total over the whole table.
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class JobPostingSearchEndpointTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private JobPostingsDatabase Db => new(fixture.ConnectionString);

    private static string NewEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private static string Marker() => Guid.NewGuid().ToString("N");

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    private async Task<(AuthApiClient Client, string DisplayName)> NewCompanyAsync(string displayName)
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync(displayName, NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        return (client, displayName);
    }

    private static async Task<Guid> ReadPostingIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JobPostingDto>();
        return Guid.Parse(body!.Id);
    }

    private async Task<PageDto<JobPostingSearchResultDto>> SearchAsync(string? query = null, int? page = null, int? pageSize = null)
    {
        using var response = await NewClient().SearchPostingsAsync(query, page, pageSize);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<PageDto<JobPostingSearchResultDto>>();
        Assert.NotNull(body);
        return body!;
    }

    // ---- Row: Browse, no keyword -----------------------------------------

    [Fact]
    public async Task Browse_with_no_keyword_returns_postings_newest_first()
    {
        var marker = Marker();
        var (client, displayName) = await NewCompanyAsync("Acme Inc.");

        using var first = await client.CreatePostingAsync($"Older {marker} role", "First posted.");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstId = await ReadPostingIdAsync(first);

        using var second = await client.CreatePostingAsync($"Newer {marker} role", "Second posted.");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondId = await ReadPostingIdAsync(second);

        // No `query` at all: a plain browse listing (Intent: missing/empty query
        // means no filter). pageSize is the max (100) so both fresh rows - being
        // the newest in the whole table - are certain to land on page 1.
        var page = await SearchAsync(page: 1, pageSize: 100);

        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);

        var firstIndex = page.Items.ToList().FindIndex(i => i.Id == firstId.ToString());
        var secondIndex = page.Items.ToList().FindIndex(i => i.Id == secondId.ToString());
        Assert.True(firstIndex >= 0, "The older posting did not appear in the browse listing.");
        Assert.True(secondIndex >= 0, "The newer posting did not appear in the browse listing.");
        Assert.True(secondIndex < firstIndex, "Rows are not ordered newest-first.");
        Assert.Equal(displayName, page.Items[secondIndex].CompanyName);
    }

    // ---- Row: An explicit empty `query` is treated the same as omitting it --

    [Fact]
    public async Task Search_with_an_explicit_empty_query_string_is_treated_as_no_filter()
    {
        // The matrix's "no postings exist at all" row can't be constructed
        // against the shared fixture (other tests' rows are already present),
        // but its point - an explicit empty `query` behaves exactly like a
        // missing one (Intent: "missing/empty means no filter") - is testable:
        // a freshly created posting must still surface under an explicit
        // `query=` just as it would with no `query` param at all.
        var marker = Marker();
        var (client, displayName) = await NewCompanyAsync("Globex Corp.");
        using var created = await client.CreatePostingAsync($"Role {marker}", "Explicit empty query still browses.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = await ReadPostingIdAsync(created);

        var page = await SearchAsync(query: string.Empty, page: 1, pageSize: 100);

        var match = page.Items.SingleOrDefault(i => i.Id == id.ToString());
        Assert.NotNull(match);
        Assert.Equal(displayName, match!.CompanyName);
    }

    // ---- Row: Keyword matches some -----------------------------------------

    [Fact]
    public async Task Keyword_search_returns_only_matching_rows_with_the_resolved_company_name()
    {
        var marker = Marker();
        var (client, displayName) = await NewCompanyAsync("Initech LLC");

        using var matching = await client.CreatePostingAsync($"Senior Engineer {marker}", "Build things.");
        Assert.Equal(HttpStatusCode.OK, matching.StatusCode);
        var matchingId = await ReadPostingIdAsync(matching);

        using var nonMatching = await client.CreatePostingAsync("Unrelated role", "No marker here.");
        Assert.Equal(HttpStatusCode.OK, nonMatching.StatusCode);
        var nonMatchingId = await ReadPostingIdAsync(nonMatching);

        var page = await SearchAsync(query: marker);

        var ids = page.Items.Select(i => i.Id).ToList();
        Assert.Contains(matchingId.ToString(), ids);
        Assert.DoesNotContain(nonMatchingId.ToString(), ids);

        var match = page.Items.Single(i => i.Id == matchingId.ToString());
        Assert.Equal($"Senior Engineer {marker}", match.Title);
        Assert.Equal("Build things.", match.Description);
        Assert.Equal(displayName, match.CompanyName);
    }

    // ---- The OR branch: a keyword present only in `description` still matches --

    [Fact]
    public async Task Keyword_matching_only_the_description_field_is_still_returned()
    {
        var marker = Marker();
        var (client, displayName) = await NewCompanyAsync("Umbrella Foods");

        using var created = await client.CreatePostingAsync(
            "Role with no marker in the title", $"The description mentions {marker} only.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = await ReadPostingIdAsync(created);

        var page = await SearchAsync(query: marker);

        var match = page.Items.SingleOrDefault(i => i.Id == id.ToString());
        Assert.NotNull(match);
        Assert.Equal(displayName, match!.CompanyName);
    }

    // ---- Search is case-insensitive ----------------------------------------

    [Fact]
    public async Task Search_matches_regardless_of_the_keywords_case()
    {
        var marker = Marker();
        var (client, _) = await NewCompanyAsync("Case Insensitive Co.");
        var needle = $"MixedCase{marker}Marker";

        using var created = await client.CreatePostingAsync($"Role {needle}", "Case-insensitivity check.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = await ReadPostingIdAsync(created);

        // The stored value is mixed-case; search with the opposite case.
        var page = await SearchAsync(query: needle.ToUpperInvariant());

        Assert.Contains(id.ToString(), page.Items.Select(i => i.Id));
    }

    // ---- Row: Keyword matches none (postings exist) ------------------------

    [Fact]
    public async Task Keyword_matching_no_posting_returns_an_empty_page_with_zero_total()
    {
        var marker = Marker();

        var page = await SearchAsync(query: marker);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    // ---- Row: Literal `%`/`_` in keyword is escaped, not a wildcard --------

    [Fact]
    public async Task Literal_percent_in_the_keyword_matches_only_the_literal_substring()
    {
        var marker = Marker();
        var (client, _) = await NewCompanyAsync("Umbrella Co.");

        // Contains the literal substring "50%".
        using var withLiteralPercent = await client.CreatePostingAsync(
            $"Role {marker}", "Get a 50% raise this quarter.");
        Assert.Equal(HttpStatusCode.OK, withLiteralPercent.StatusCode);
        var literalPercentId = await ReadPostingIdAsync(withLiteralPercent);

        // Contains "50" but never followed by a literal "%" - would incorrectly
        // match "50%" as an unescaped LIKE pattern (% as wildcard swallows the
        // rest of the string), but must not match once % is escaped.
        using var withoutLiteralPercent = await client.CreatePostingAsync(
            $"Role {marker}", "We grew headcount to 5000 people.");
        Assert.Equal(HttpStatusCode.OK, withoutLiteralPercent.StatusCode);
        var withoutLiteralPercentId = await ReadPostingIdAsync(withoutLiteralPercent);

        var page = await SearchAsync(query: "50%");

        var ids = page.Items.Select(i => i.Id).ToList();
        Assert.Contains(literalPercentId.ToString(), ids);
        Assert.DoesNotContain(withoutLiteralPercentId.ToString(), ids);
    }

    // ---- Row: Page beyond the last page ------------------------------------

    [Fact]
    public async Task Requesting_a_page_beyond_the_last_page_returns_an_empty_page_but_echoes_the_true_total()
    {
        var marker = Marker();
        var (client, _) = await NewCompanyAsync("Stark Industries");
        using (var created = await client.CreatePostingAsync($"Role A {marker}", "First."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using (var created = await client.CreatePostingAsync($"Role B {marker}", "Second."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        var page = await SearchAsync(query: marker, page: 999);

        Assert.Empty(page.Items);
        Assert.Equal(999, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(2, page.Total);
    }

    // ---- Acceptance Criteria: page=2 returns the correctly offset rows -----

    [Fact]
    public async Task Requesting_page_2_returns_the_next_pages_rows_offset_by_page_size()
    {
        var marker = Marker();
        var (client, _) = await NewCompanyAsync("Wayne Enterprises");

        using (var created = await client.CreatePostingAsync($"Role 1 {marker}", "Oldest."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using (var created = await client.CreatePostingAsync($"Role 2 {marker}", "Middle."))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using var newest = await client.CreatePostingAsync($"Role 3 {marker}", "Newest.");
        Assert.Equal(HttpStatusCode.OK, newest.StatusCode);
        var newestId = await ReadPostingIdAsync(newest);

        // pageSize=2: page 1 holds the two newest rows (Role 3, Role 2); page 2
        // holds only the oldest, Role 1.
        var firstPage = await SearchAsync(query: marker, page: 1, pageSize: 2);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(newestId.ToString(), firstPage.Items[0].Id);
        Assert.Equal(3, firstPage.Total);

        var secondPage = await SearchAsync(query: marker, page: 2, pageSize: 2);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(3, secondPage.Total);
        Assert.Equal("Role 1 " + marker, secondPage.Items[0].Title);
    }

    // ---- Row: pageSize out of range is clamped -----------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    public async Task Out_of_range_page_size_is_clamped_to_the_default(int requestedPageSize)
    {
        var page = await SearchAsync(query: Marker(), pageSize: requestedPageSize);

        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task A_page_number_below_1_is_clamped_to_1()
    {
        var page = await SearchAsync(query: Marker(), page: 0);

        Assert.Equal(1, page.Page);
    }

    // ---- Row: Owner Company unresolved -> the row is dropped, not the page -

    [Fact]
    public async Task A_posting_whose_owner_company_cannot_be_resolved_is_dropped_but_still_counted_in_total()
    {
        var marker = Marker();
        var (client, displayName) = await NewCompanyAsync("Wonka Industries");
        using var real = await client.CreatePostingAsync($"Real role {marker}", "A real posting.");
        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        var realId = await ReadPostingIdAsync(real);

        // An orphan posting: owner_company_id has no company_account row. Only
        // reachable by a direct insert - every API-created posting's owner is a
        // real authenticated Company (unreachable in v1: no company deletion).
        var orphanId = await Db.InsertPostingAsync(
            Guid.NewGuid(), $"Ghost role {marker}", "Nobody owns this posting.");

        var page = await SearchAsync(query: marker);

        // The DB match count includes the orphan row; the projected page drops it.
        Assert.Equal(2, page.Total);
        var ids = page.Items.Select(i => i.Id).ToList();
        Assert.Contains(realId.ToString(), ids);
        Assert.DoesNotContain(orphanId.ToString(), ids);
        Assert.Equal(displayName, page.Items.Single(i => i.Id == realId.ToString()).CompanyName);
    }

    // ---- Row: Multi-company page -------------------------------------------

    [Fact]
    public async Task A_page_with_postings_from_two_different_companies_resolves_both_company_names()
    {
        var marker = Marker();
        var (clientA, displayNameA) = await NewCompanyAsync("Aperture Science");
        var (clientB, displayNameB) = await NewCompanyAsync("Black Mesa");

        using var postingA = await clientA.CreatePostingAsync($"Role from A {marker}", "Posted by company A.");
        Assert.Equal(HttpStatusCode.OK, postingA.StatusCode);
        var postingAId = await ReadPostingIdAsync(postingA);

        using var postingB = await clientB.CreatePostingAsync($"Role from B {marker}", "Posted by company B.");
        Assert.Equal(HttpStatusCode.OK, postingB.StatusCode);
        var postingBId = await ReadPostingIdAsync(postingB);

        var page = await SearchAsync(query: marker);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(displayNameA, page.Items.Single(i => i.Id == postingAId.ToString()).CompanyName);
        Assert.Equal(displayNameB, page.Items.Single(i => i.Id == postingBId.ToString()).CompanyName);
    }
}

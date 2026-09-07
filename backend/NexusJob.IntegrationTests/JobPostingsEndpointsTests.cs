using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 2.1a's I/O &amp; Edge-Case Matrix for
/// <c>POST /api/job-postings</c>, run against a real Postgres (Testcontainers)
/// through the shared <see cref="IdentityApiFixture"/> (which boots all three
/// modules and runs the Host startup migration, so
/// <c>job_postings.job_posting</c> exists before the first request).
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class JobPostingsEndpointsTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private JobPostingsDatabase Db => new(fixture.ConnectionString);

    private static string NewEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    private async Task<(AuthApiClient Client, Guid AccountId)> NewCompanyAsync()
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync("Acme Inc.", NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var body = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();
        return (client, Guid.Parse(body!.Id));
    }

    // ---- Row: Create a posting (happy path) ------------------------------

    [Fact]
    public async Task Create_posting_as_a_company_returns_200_with_the_representation_and_writes_exactly_one_row()
    {
        var (client, companyId) = await NewCompanyAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var response = await client.CreatePostingAsync("Senior .NET Engineer", "Build the NexusJob core loop.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // No envelope, no Location (spec Resolved Decisions: 200 with the
        // representation, not 201): the body is exactly { id, title, description, createdAt }.
        Assert.Null(response.Headers.Location);
        using (var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var names = payload.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(["createdAt", "description", "id", "title"], names);
        }

        var body = await response.Content.ReadFromJsonAsync<JobPostingDto>();
        Assert.NotNull(body);
        Assert.True(Guid.TryParse(body!.Id, out var postingId));
        Assert.Equal(7, postingId.Version); // Guid.CreateVersion7
        Assert.Equal("Senior .NET Engineer", body.Title);
        Assert.Equal("Build the NexusJob core loop.", body.Description);
        Assert.True((DateTimeOffset.UtcNow - body.CreatedAt).Duration() < TimeSpan.FromMinutes(2));

        Assert.Equal(before + 1, await Db.CountPostingsForOwnerAsync(companyId));

        var row = await Db.GetPostingAsync(postingId);
        Assert.NotNull(row);
        Assert.Equal(companyId, row!.OwnerCompanyId);
        Assert.Equal("Senior .NET Engineer", row.Title);
        Assert.Equal("Build the NexusJob core loop.", row.Description);
        Assert.Equal(DateTimeOffset.UtcNow.UtcDateTime, row.CreatedAt.UtcDateTime, TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.Zero, row.CreatedAt.Offset); // stored as UTC timestamptz
    }

    [Fact]
    public async Task Create_posting_trims_the_title_and_description_before_storing_them()
    {
        var (client, companyId) = await NewCompanyAsync();

        using var response = await client.CreatePostingAsync("  Padded Title  ", "\tPadded description\n");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobPostingDto>();
        Assert.Equal("Padded Title", body!.Title);
        Assert.Equal("Padded description", body.Description);

        var row = await Db.GetPostingAsync(Guid.Parse(body.Id));
        Assert.Equal("Padded Title", row!.Title);
        Assert.Equal("Padded description", row.Description);
        Assert.Equal(companyId, row.OwnerCompanyId);
    }

    // ---- Row: Empty / whitespace title or description ------------------

    [Theory]
    [InlineData("", "A valid description.", "title")]
    [InlineData("   ", "A valid description.", "title")]
    [InlineData("A valid title", "", "description")]
    [InlineData("A valid title", "   ", "description")]
    public async Task Create_posting_with_a_blank_title_or_description_returns_400_naming_the_field_and_writes_no_row(
        string title, string description, string invalidField)
    {
        var (client, companyId) = await NewCompanyAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var response = await client.CreatePostingAsync(title, description);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains(invalidField, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(before, await Db.CountPostingsForOwnerAsync(companyId));
    }

    // ---- Row: Over-length title or description -----------------------

    [Theory]
    [InlineData(201, 100, "title")]
    [InlineData(100, 4001, "description")]
    public async Task Create_posting_with_an_over_length_field_returns_400_naming_the_field_and_writes_no_row(
        int titleLength, int descriptionLength, string invalidField)
    {
        var (client, companyId) = await NewCompanyAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var response = await client.CreatePostingAsync(new string('T', titleLength), new string('D', descriptionLength));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains(invalidField, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(before, await Db.CountPostingsForOwnerAsync(companyId));
    }

    // ---- Row: two Companies' postings stay isolated by owner --------

    [Fact]
    public async Task Postings_are_isolated_per_owning_company()
    {
        var (companyA, idA) = await NewCompanyAsync();
        var (companyB, idB) = await NewCompanyAsync();
        Assert.NotEqual(idA, idB);

        using var createdByA = await companyA.CreatePostingAsync("A role", "Posted by Company A.");
        Assert.Equal(HttpStatusCode.OK, createdByA.StatusCode);
        var postingA = await createdByA.Content.ReadFromJsonAsync<JobPostingDto>();

        using (var createdByB = await companyB.CreatePostingAsync("B role", "Posted by Company B."))
        {
            Assert.Equal(HttpStatusCode.OK, createdByB.StatusCode);
        }

        Assert.Equal(1, await Db.CountPostingsForOwnerAsync(idA));
        Assert.Equal(1, await Db.CountPostingsForOwnerAsync(idB));

        var rowA = await Db.GetPostingAsync(Guid.Parse(postingA!.Id));
        Assert.Equal(idA, rowA!.OwnerCompanyId);
    }

    // ---- Row: Unauthenticated caller ---------------------------------

    [Fact]
    public async Task Create_posting_without_an_auth_cookie_returns_401_problem_details_and_writes_no_row()
    {
        var client = NewClient();
        var before = await Db.CountAllPostingsAsync();

        // A valid antiforgery token is seeded, so the 401 is purely the missing
        // session (RequireAuthorization), not the antiforgery filter.
        using var response = await client.CreatePostingAsync("Ghost role", "Nobody is signed in.");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(before, await Db.CountAllPostingsAsync());
    }

    // ---- Row: Job Seeker session -----------------------------------

    [Fact]
    public async Task Create_posting_with_a_job_seeker_session_returns_403_problem_details_and_writes_no_row()
    {
        var client = NewClient();
        using (var registration = await client.RegisterAsync(
            "Dana Scully", NewEmail(), Password, accountType: "job_seeker"))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        var before = await Db.CountAllPostingsAsync();

        using var response = await client.CreatePostingAsync("Not for seekers", "Company-only endpoint.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(before, await Db.CountAllPostingsAsync());
    }

    // ---- Row: Missing / invalid antiforgery token -----------------

    [Fact]
    public async Task Create_posting_without_an_antiforgery_token_returns_400_and_writes_no_row()
    {
        var (client, companyId) = await NewCompanyAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var response = await client.CreatePostingAsync("No token", "Missing X-CSRF-TOKEN.", csrfToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(before, await Db.CountPostingsForOwnerAsync(companyId));
    }

    [Fact]
    public async Task Create_posting_with_a_garbage_antiforgery_token_returns_400_and_writes_no_row()
    {
        var (client, companyId) = await NewCompanyAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var response = await client.CreatePostingAsync("Bad token", "Invalid X-CSRF-TOKEN.", csrfToken: "not-a-real-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(before, await Db.CountPostingsForOwnerAsync(companyId));
    }

    // ---- Row: Missing / malformed body -------------------------

    [Fact]
    public async Task Create_posting_with_an_empty_body_returns_400_not_500_and_writes_no_row()
    {
        var (client, companyId) = await NewCompanyAsync();
        var token = await client.GetCsrfTokenAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/job-postings")
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await Db.CountPostingsForOwnerAsync(companyId));
    }

    [Fact]
    public async Task Create_posting_with_an_empty_json_object_returns_400_validation_problem_and_writes_no_row()
    {
        var (client, companyId) = await NewCompanyAsync();
        var token = await client.GetCsrfTokenAsync();
        var before = await Db.CountPostingsForOwnerAsync(companyId);

        using var response = await client.PostAsync("/api/job-postings", new { }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Equal(before, await Db.CountPostingsForOwnerAsync(companyId));
    }

    // ---- Row: structured logs during a create carry no secrets ----

    [Fact]
    public async Task No_cookie_token_or_body_content_appears_in_the_structured_logs_for_a_create()
    {
        var (client, _) = await NewCompanyAsync();
        var token = await client.GetCsrfTokenAsync();

        const string title = "Confidential Widget Wrangler";
        const string description = "Secret sauce recipe maintenance.";
        using (var response = await client.CreatePostingAsync(title, description, token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var snapshot = fixture.Logs.Snapshot();

        // Guard against a vacuous pass: the create must have produced a log line.
        Assert.Contains(snapshot, line => line.Contains("/api/job-postings", StringComparison.Ordinal));

        foreach (var line in snapshot)
        {
            Assert.DoesNotContain(title, line, StringComparison.Ordinal);
            Assert.DoesNotContain(description, line, StringComparison.Ordinal);
            Assert.DoesNotContain(token, line, StringComparison.Ordinal);
            Assert.DoesNotContain("Set-Cookie", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("X-CSRF-TOKEN", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- Row: Migration applied -- job_posting shape + history ----

    [Fact]
    public async Task InitialJobPostings_migration_created_job_posting_in_the_job_postings_schema_with_its_own_history()
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var columns = new Dictionary<string, (string DataType, bool NotNull)>(StringComparer.Ordinal);
        await using (var command = new Npgsql.NpgsqlCommand(
            """
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'job_postings' AND table_name = 'job_posting'
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns[reader.GetString(0)] = (
                    reader.GetString(1),
                    string.Equals(reader.GetString(2), "NO", StringComparison.OrdinalIgnoreCase));
            }
        }

        Assert.Equal(
            ["created_at", "description", "id", "owner_company_id", "title"],
            columns.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("uuid", columns["id"].DataType);
        Assert.Equal("uuid", columns["owner_company_id"].DataType);
        Assert.Equal("character varying", columns["title"].DataType);
        Assert.Equal("character varying", columns["description"].DataType);
        Assert.Equal("timestamp with time zone", columns["created_at"].DataType);
        Assert.All(columns.Values, c => Assert.True(c.NotNull));

        // A non-unique index covers owner_company_id (list/tenant lookups).
        await using (var command = new Npgsql.NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (i.indkey)
            WHERE n.nspname = 'job_postings' AND t.relname = 'job_posting'
              AND NOT i.indisunique AND i.indnatts = 1 AND a.attname = 'owner_company_id'
            """, connection))
        {
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1);
        }

        // id is the primary key.
        await using (var command = new Npgsql.NpgsqlCommand(
            """
            SELECT count(*)
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = 'job_postings' AND tc.table_name = 'job_posting'
              AND tc.constraint_type = 'PRIMARY KEY' AND kcu.column_name = 'id'
            """, connection))
        {
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1);
        }

        // No foreign key crosses out of the job_postings schema (AD-7).
        await using (var command = new Npgsql.NpgsqlCommand(
            """
            SELECT count(*)
            FROM information_schema.table_constraints
            WHERE table_schema = 'job_postings' AND table_name = 'job_posting'
              AND constraint_type = 'FOREIGN KEY'
            """, connection))
        {
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }

        // JobPostings owns its own migration history in the job_postings schema;
        // identity's is untouched.
        Assert.True(await MigrationApplied("job_postings", "InitialJobPostings"));
        Assert.False(await MigrationApplied("identity", "InitialJobPostings"));

        await using (var command = new Npgsql.NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'job_posting'",
            connection))
        {
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
    }

    // ---- Row: Host starts with the database reachable -----------

    [Fact]
    public async Task Host_started_with_a_reachable_database_migrated_job_postings_and_health_is_200()
    {
        using var client = fixture.CreateClient();

        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        Assert.True(await MigrationApplied("job_postings", "InitialJobPostings"));
    }

    private async Task<bool> MigrationApplied(string schema, string migrationSuffix)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            $"""
             SELECT count(*) FROM information_schema.tables
             WHERE table_schema = '{schema}' AND table_name = '__EFMigrationsHistory'
             """, connection);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            return false;
        }

        await using var historyCommand = new Npgsql.NpgsqlCommand(
            $"SELECT count(*) FROM \"{schema}\".\"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE @suffix", connection);
        historyCommand.Parameters.AddWithValue("suffix", "%" + migrationSuffix);
        return Convert.ToInt32(await historyCommand.ExecuteScalarAsync()) >= 1;
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

        public IDictionary<string, string[]>? Errors { get; init; }
    }

    private sealed record ValidationProblemBody
    {
        public IDictionary<string, string[]>? Errors { get; init; }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of spec 3.1a's I/O &amp; Edge-Case Matrix for
/// <c>POST /api/applications</c> and <c>GET /api/applications/mine</c>, run
/// against a real Postgres (Testcontainers) through the shared
/// <see cref="IdentityApiFixture"/> (which boots all three modules and runs the
/// Host startup migration, so <c>applications.application</c> exists before the
/// first request).
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class ApplicationsEndpointsTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private ApplicationsDatabase Db => new(fixture.ConnectionString);

    private static string NewEmail() => $"u-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    /// <summary>Registers a Company and creates one posting; returns the owning-company and posting ids.</summary>
    private async Task<(Guid CompanyId, Guid PostingId)> NewCompanyWithPostingAsync()
    {
        var client = NewClient();
        using var registration = await client.RegisterAsync("Acme Inc.", NewEmail(), Password);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var company = await registration.Content.ReadFromJsonAsync<AuthAccountDto>();

        using var created = await client.CreatePostingAsync("Senior .NET Engineer", "Build the NexusJob core loop.");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var posting = await created.Content.ReadFromJsonAsync<JobPostingDto>();

        return (Guid.Parse(company!.Id), Guid.Parse(posting!.Id));
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

    // ---- Row: First apply ------------------------------------------------

    [Fact]
    public async Task First_apply_returns_200_with_the_representation_and_writes_exactly_one_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, seekerId) = await NewJobSeekerAsync();

        using var response = await seeker.ApplyAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);

        using (var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var names = payload.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(["id", "jobPostingId", "submittedAt"], names);
        }

        var body = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.NotNull(body);
        Assert.True(Guid.TryParse(body!.Id, out var applicationId));
        Assert.Equal(7, applicationId.Version); // Guid.CreateVersion7
        Assert.Equal(postingId.ToString(), body.JobPostingId);
        Assert.True((DateTimeOffset.UtcNow - body.SubmittedAt).Duration() < TimeSpan.FromMinutes(2));

        Assert.Equal(1, await Db.CountApplicationsForPostingAsync(postingId));
        Assert.Equal(1, await Db.CountApplicationsForSeekerAsync(seekerId));

        var row = await Db.GetApplicationRowAsync(postingId, seekerId);
        Assert.NotNull(row);
        Assert.Equal(applicationId, row!.Value.Id);
        Assert.Equal(body.SubmittedAt, row.Value.SubmittedAt, TimeSpan.FromMilliseconds(1));
    }

    // ---- Row: Repeat apply --------------------------------------------

    [Fact]
    public async Task Repeat_apply_returns_200_with_the_existing_application_and_writes_no_second_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, seekerId) = await NewJobSeekerAsync();

        using var first = await seeker.ApplyAsync(postingId.ToString());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<ApplicationDto>();

        using var second = await seeker.ApplyAsync(postingId.ToString());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<ApplicationDto>();

        Assert.Equal(firstBody!.Id, secondBody!.Id);
        Assert.Equal(firstBody.JobPostingId, secondBody.JobPostingId);
        Assert.Equal(firstBody.SubmittedAt, secondBody.SubmittedAt, TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, await Db.CountApplicationsForPostingAsync(postingId));
        Assert.Equal(1, await Db.CountApplicationsForSeekerAsync(seekerId));
    }

    // ---- Row: Concurrent duplicates ---------------------------------

    [Fact]
    public async Task Concurrent_duplicate_applies_all_return_200_with_the_same_id_and_write_exactly_one_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, seekerId) = await NewJobSeekerAsync();

        // Seed one antiforgery token, then fire the applies concurrently so every
        // request but one loses the race with the unique constraint.
        var token = await seeker.GetCsrfTokenAsync();
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => seeker.ApplyAsync(postingId.ToString(), token)));

        try
        {
            var ids = new List<string>();
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<ApplicationDto>();
                ids.Add(body!.Id);
            }

            Assert.Single(ids.Distinct());
            Assert.Equal(1, await Db.CountApplicationsForPostingAsync(postingId));
            Assert.Equal(1, await Db.CountApplicationsForSeekerAsync(seekerId));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- Two different seekers apply to the same posting ------------

    [Fact]
    public async Task Two_different_seekers_can_each_apply_to_the_same_posting_and_both_rows_are_written()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seekerA, seekerIdA) = await NewJobSeekerAsync();
        var (seekerB, seekerIdB) = await NewJobSeekerAsync();
        Assert.NotEqual(seekerIdA, seekerIdB);

        using var responseA = await seekerA.ApplyAsync(postingId.ToString());
        using var responseB = await seekerB.ApplyAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

        var bodyA = await responseA.Content.ReadFromJsonAsync<ApplicationDto>();
        var bodyB = await responseB.Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.NotEqual(bodyA!.Id, bodyB!.Id);

        // The unique constraint is on the pair, not on job_posting_id alone.
        Assert.Equal(2, await Db.CountApplicationsForPostingAsync(postingId));
        Assert.Equal(1, await Db.CountApplicationsForSeekerAsync(seekerIdA));
        Assert.Equal(1, await Db.CountApplicationsForSeekerAsync(seekerIdB));
    }

    // ---- Mine is scoped to (posting, seeker), not seeker alone -----

    [Fact]
    public async Task Mine_returns_applied_false_for_a_different_posting_the_seeker_has_not_applied_to()
    {
        var (_, postingA) = await NewCompanyWithPostingAsync();
        var (_, postingB) = await NewCompanyWithPostingAsync();
        Assert.NotEqual(postingA, postingB);

        var (seeker, _) = await NewJobSeekerAsync();

        using (var apply = await seeker.ApplyAsync(postingA.ToString()))
        {
            Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        }

        // Same seeker, a different real posting they have not applied to: the
        // mine query filters on job_posting_id AND job_seeker_id.
        using var response = await seeker.GetMyApplicationAsync(postingB.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MyApplicationDto>();
        Assert.False(body!.Applied);
        Assert.Null(body.AppliedAt);
    }

    // ---- Row: Unknown posting -------------------------------------

    [Fact]
    public async Task Apply_to_an_unknown_posting_returns_404_problem_details_and_writes_no_row()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var unknownPostingId = Guid.NewGuid();

        using var response = await seeker.ApplyAsync(unknownPostingId.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(0, await Db.CountApplicationsForSeekerAsync(seekerId));
        Assert.Equal(0, await Db.CountApplicationsForPostingAsync(unknownPostingId));
    }

    // ---- Row: Missing / unparseable jobPostingId ------------------

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("   ")]
    public async Task Apply_with_a_missing_or_unparseable_job_posting_id_returns_400_naming_the_field_and_writes_no_row(
        string jobPostingId)
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();

        using var response = await seeker.ApplyAsync(jobPostingId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains("jobPostingId", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, await Db.CountApplicationsForSeekerAsync(seekerId));
    }

    [Fact]
    public async Task Apply_with_an_empty_json_object_returns_400_validation_problem_and_writes_no_row()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var token = await seeker.GetCsrfTokenAsync();

        using var response = await seeker.PostAsync("/api/applications", new { }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Equal(0, await Db.CountApplicationsForSeekerAsync(seekerId));
    }

    // ---- Row: Empty body ----------------------------------------

    [Fact]
    public async Task Apply_with_an_empty_body_returns_400_not_500_and_writes_no_row()
    {
        var (seeker, seekerId) = await NewJobSeekerAsync();
        var token = await seeker.GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/applications")
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await seeker.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Db.CountApplicationsForSeekerAsync(seekerId));
    }

    // ---- Row: Anonymous --------------------------------------

    [Fact]
    public async Task Apply_without_an_auth_cookie_returns_401_problem_details_and_writes_no_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var before = await Db.CountApplicationsForPostingAsync(postingId);

        // A valid antiforgery token is seeded, so the 401 is purely the missing
        // session (RequireAuthorization), not the antiforgery filter.
        using var response = await NewClient().ApplyAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(before, await Db.CountApplicationsForPostingAsync(postingId));
    }

    // ---- Row: Company session ------------------------------

    [Fact]
    public async Task Apply_with_a_company_session_returns_403_problem_details_and_writes_no_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();

        var company = NewClient();
        using (var registration = await company.RegisterAsync("Globex Corp.", NewEmail(), Password))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        var before = await Db.CountApplicationsForPostingAsync(postingId);

        using var response = await company.ApplyAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(before, await Db.CountApplicationsForPostingAsync(postingId));
    }

    // ---- Row: Missing / garbage CSRF ----------------------

    [Fact]
    public async Task Apply_without_an_antiforgery_token_returns_400_and_writes_no_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, seekerId) = await NewJobSeekerAsync();

        using var response = await seeker.ApplyAsync(postingId.ToString(), csrfToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(0, await Db.CountApplicationsForSeekerAsync(seekerId));
    }

    [Fact]
    public async Task Apply_with_a_garbage_antiforgery_token_returns_400_and_writes_no_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, seekerId) = await NewJobSeekerAsync();

        using var response = await seeker.ApplyAsync(postingId.ToString(), csrfToken: "not-a-real-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(0, await Db.CountApplicationsForSeekerAsync(seekerId));
    }

    // ---- Row: Mine -- applied ---------------------------

    [Fact]
    public async Task Mine_returns_applied_true_with_the_timestamp_when_the_caller_has_a_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, seekerId) = await NewJobSeekerAsync();

        using (var apply = await seeker.ApplyAsync(postingId.ToString()))
        {
            Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        }

        using var response = await seeker.GetMyApplicationAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MyApplicationDto>();
        Assert.NotNull(body);
        Assert.True(body!.Applied);
        Assert.NotNull(body.AppliedAt);

        var row = await Db.GetApplicationRowAsync(postingId, seekerId);
        Assert.Equal(row!.Value.SubmittedAt, body.AppliedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    // ---- Row: Mine -- not applied ----------------------

    [Fact]
    public async Task Mine_returns_applied_false_with_a_null_timestamp_when_the_caller_has_no_row()
    {
        var (_, postingId) = await NewCompanyWithPostingAsync();
        var (seeker, _) = await NewJobSeekerAsync();

        using var response = await seeker.GetMyApplicationAsync(postingId.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // "appliedAt": null is always present on the wire (spec Design Notes).
        using (var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var names = payload.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(["applied", "appliedAt"], names);
            Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("appliedAt").ValueKind);
        }

        var body = await response.Content.ReadFromJsonAsync<MyApplicationDto>();
        Assert.False(body!.Applied);
        Assert.Null(body.AppliedAt);
    }

    // ---- Row: Mine -- bogus posting, not applied ------

    [Fact]
    public async Task Mine_returns_applied_false_for_an_unknown_posting_id_the_caller_never_applied_to()
    {
        var (seeker, _) = await NewJobSeekerAsync();

        using var response = await seeker.GetMyApplicationAsync(Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MyApplicationDto>();
        Assert.False(body!.Applied);
        Assert.Null(body.AppliedAt);
    }

    // ---- Row: Mine -- missing / non-GUID param -------

    [Fact]
    public async Task Mine_with_no_job_posting_id_returns_400_validation_problem()
    {
        var (seeker, _) = await NewJobSeekerAsync();

        using var response = await seeker.GetMyApplicationAsync(null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains("jobPostingId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Mine_with_a_non_guid_job_posting_id_returns_400_validation_problem()
    {
        var (seeker, _) = await NewJobSeekerAsync();

        using var response = await seeker.GetMyApplicationAsync("not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains("jobPostingId", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Row: Mine -- anonymous / company -----------

    [Fact]
    public async Task Mine_without_an_auth_cookie_returns_401_problem_details()
    {
        using var response = await NewClient().GetMyApplicationAsync(Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Mine_with_a_company_session_returns_403_problem_details()
    {
        var company = NewClient();
        using (var registration = await company.RegisterAsync("Initech LLC", NewEmail(), Password))
        {
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        }

        using var response = await company.GetMyApplicationAsync(Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    // ---- Row: Migration applied -- application shape + history ----

    [Fact]
    public async Task InitialApplications_migration_created_application_in_the_applications_schema_with_its_own_history()
    {
        var (columns, hasUniquePairIndex) = await Db.GetApplicationSchemaAsync();

        var byName = columns.ToDictionary(c => c.Name, c => (c.DataType, c.NotNull), StringComparer.Ordinal);
        Assert.Equal(
            ["id", "job_posting_id", "job_seeker_id", "submitted_at"],
            byName.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("uuid", byName["id"].DataType);
        Assert.Equal("uuid", byName["job_posting_id"].DataType);
        Assert.Equal("uuid", byName["job_seeker_id"].DataType);
        Assert.Equal("timestamp with time zone", byName["submitted_at"].DataType);
        Assert.All(byName.Values, c => Assert.True(c.NotNull));

        Assert.True(hasUniquePairIndex, "Expected a unique index on (job_posting_id, job_seeker_id).");

        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        // id is the primary key.
        await using (var command = new Npgsql.NpgsqlCommand(
            """
            SELECT count(*)
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = 'applications' AND tc.table_name = 'application'
              AND tc.constraint_type = 'PRIMARY KEY' AND kcu.column_name = 'id'
            """, connection))
        {
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1);
        }

        // No foreign key crosses out of the applications schema (AD-7).
        await using (var command = new Npgsql.NpgsqlCommand(
            """
            SELECT count(*)
            FROM information_schema.table_constraints
            WHERE table_schema = 'applications' AND table_name = 'application'
              AND constraint_type = 'FOREIGN KEY'
            """, connection))
        {
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }

        // Applications owns its own migration history in the applications schema;
        // identity's and job_postings' are untouched.
        Assert.True(await MigrationApplied("applications", "InitialApplications"));
        Assert.False(await MigrationApplied("identity", "InitialApplications"));
        Assert.False(await MigrationApplied("job_postings", "InitialApplications"));

        await using (var command = new Npgsql.NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'application'",
            connection))
        {
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
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

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// One test per row of the spec's I/O &amp; Edge-Case Matrix for
/// <c>/api/auth/*</c>, run against a real Postgres (Testcontainers). "Host starts
/// with the database down" is in <see cref="HostStartupWithDatabaseDownTests"/>
/// (no container needed).
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class AuthEndpointsTests(IdentityApiFixture fixture)
{
    private const string Password = "Sup3rSecret!";

    private IdentityDatabase Db => new(fixture.ConnectionString);

    private static string NewEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private AuthApiClient NewClient() => new(fixture.CreateClient());

    // ---- Row: Register a new Company --------------------------------------

    [Fact]
    public async Task Register_new_company_returns_200_with_summary_a_scoped_cookie_and_one_hashed_row()
    {
        var client = NewClient();
        var email = NewEmail();

        using var response = await client.RegisterAsync("Acme Inc.", email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthAccountDto>();
        Assert.NotNull(body);
        Assert.True(Guid.TryParse(body!.Id, out _));
        Assert.Equal("company", body.AccountType);
        Assert.Equal("Acme Inc.", body.DisplayName);

        var setCookie = Assert.Single(AuthCookies(response));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, await Db.CountCompanyAccountsAsync(email));
        var row = await Db.GetCompanyAccountAsync(email);
        Assert.NotNull(row);
        Assert.Equal(email, row!.Value.Email); // stored lower-cased
        Assert.NotEqual(Password, row.Value.PasswordHash);
        Assert.True(row.Value.PasswordHash.Length > 40); // PBKDF2 base64 blob
        Assert.True(IsBase64(row.Value.PasswordHash));
    }

    // ---- Row: Register, email already a Company (case-insensitive) -------

    [Fact]
    public async Task Register_with_an_existing_email_returns_409_problem_details_and_writes_no_second_row()
    {
        var email = NewEmail();
        using (var first = await NewClient().RegisterAsync("Acme Inc.", email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var client = NewClient();
        using var response = await client.RegisterAsync("Acme Again", email.ToUpperInvariant(), Password);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.DoesNotContain(Password, await response.Content.ReadAsStringAsync());
        Assert.Empty(AuthCookies(response));
        Assert.Equal(1, await Db.CountCompanyAccountsAsync(email));
    }

    // ---- Row: Register, accountType not company -------------------------

    [Fact]
    public async Task Register_with_a_non_company_account_type_returns_400_problem_details_and_writes_no_row()
    {
        var client = NewClient();
        var email = NewEmail();

        using var response = await client.RegisterAsync("Some Person", email, Password, accountType: "job_seeker");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
        Assert.Equal(0, await Db.CountCompanyAccountsAsync(email));
    }

    // ---- Row: Register, invalid body ----------------------------------

    [Theory]
    [InlineData("Acme", "not-an-email", "Sup3rSecret!", "email")]
    [InlineData("Acme", "ok@example.com", "short", "password")]
    [InlineData("", "ok2@example.com", "Sup3rSecret!", "name")]
    public async Task Register_with_an_invalid_body_returns_400_validation_problem_details_naming_the_field(
        string name, string email, string password, string invalidField)
    {
        var client = NewClient();
        var token = await client.GetCsrfTokenAsync();

        using var response = await client.PostAsync(
            "/api/auth/register",
            new { accountType = "company", name, email, password },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem);
        Assert.NotNull(problem!.Errors);
        Assert.Contains(
            problem.Errors!.Keys,
            key => key.Contains(invalidField, StringComparison.OrdinalIgnoreCase));

        if (email.Contains('@'))
        {
            Assert.Equal(0, await Db.CountCompanyAccountsAsync(email));
        }
    }

    [Fact]
    public async Task Register_with_an_empty_body_returns_400_not_500()
    {
        var client = NewClient();
        var token = await client.GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Db.CountCompanyAccountsAsync(string.Empty));
    }

    // ---- Row: Login, correct credentials ------------------------------

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_with_summary_and_a_cookie()
    {
        var email = NewEmail();
        using (var reg = await NewClient().RegisterAsync("Acme Inc.", email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        }

        var client = NewClient();
        using var response = await client.LoginAsync(email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthAccountDto>();
        Assert.Equal("company", body!.AccountType);
        Assert.Equal("Acme Inc.", body.DisplayName);

        var setCookie = Assert.Single(AuthCookies(response));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_normalizes_the_email_so_a_differently_cased_address_still_matches()
    {
        var local = $"Mixed-{Guid.NewGuid():N}";
        using (var reg = await NewClient().RegisterAsync("Acme Inc.", $"{local}@Case.Example.com", Password))
        {
            Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        }

        var client = NewClient();
        using var response = await client.LoginAsync($"{local.ToLowerInvariant()}@case.example.com", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(AuthCookies(response));
    }

    // ---- Row: Login, wrong password or unknown email -----------------

    [Fact]
    public async Task Login_failure_is_one_generic_401_with_an_identical_body_for_unknown_email_and_wrong_password()
    {
        var email = NewEmail();
        using (var reg = await NewClient().RegisterAsync("Acme Inc.", email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        }

        using var wrongPassword = await NewClient().LoginAsync(email, "WrongPassword1!");
        using var unknownEmail = await NewClient().LoginAsync(NewEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Empty(AuthCookies(wrongPassword));
        Assert.Empty(AuthCookies(unknownEmail));

        // Byte-identical except for the per-request traceId extension: same title,
        // same status, and no field-level detail that would reveal which part was
        // wrong.
        var wrong = await wrongPassword.Content.ReadFromJsonAsync<ProblemBody>();
        var unknown = await unknownEmail.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal(unknown!.Title, wrong!.Title);
        Assert.Equal(unknown.Status, wrong.Status);
        Assert.Equal(401, wrong.Status);
        Assert.Null(wrong.Detail);
        Assert.Null(wrong.Errors);
    }

    // ---- Row: GET /me, signed in -----------------------------------

    [Fact]
    public async Task Me_when_signed_in_returns_the_summary_resolved_from_the_name_identifier()
    {
        var client = NewClient();
        var email = NewEmail();
        using var reg = await client.RegisterAsync("Acme Inc.", email, Password);
        var registered = await reg.Content.ReadFromJsonAsync<AuthAccountDto>();

        using var response = await client.Http.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<AuthAccountDto>();
        Assert.Equal(registered!.Id, me!.Id);
        Assert.Equal("company", me.AccountType);
        Assert.Equal("Acme Inc.", me.DisplayName);
    }

    // ---- Row: GET /me, no/invalid cookie -------------------------

    [Fact]
    public async Task Me_without_a_cookie_returns_401_problem_details()
    {
        using var response = await fixture.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Me_with_an_invalid_cookie_returns_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", "nexusjob_auth=not-a-real-token");

        using var response = await fixture.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Row: POST /logout -------------------------------------

    [Fact]
    public async Task Logout_clears_the_cookie_and_a_following_me_call_is_401()
    {
        var client = NewClient();
        var email = NewEmail();
        using (var reg = await client.RegisterAsync("Acme Inc.", email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        }

        var token = await client.GetCsrfTokenAsync();
        using var logout = await client.PostAsync("/api/auth/logout", new { }, token);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = Assert.Single(AuthCookies(logout));
        Assert.Contains("expires=", cleared, StringComparison.OrdinalIgnoreCase); // past-dated -> cleared

        using var me = await client.Http.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    // ---- Row: GET /csrf, anonymous -----------------------------

    [Fact]
    public async Task Csrf_is_anonymous_returns_a_token_and_sets_the_antiforgery_cookie()
    {
        using var response = await fixture.CreateClient().GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CsrfTokenDto>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));

        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];
        Assert.Contains(cookies, c => c.Contains("Antiforgery", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Row: state-changing call without an antiforgery token ---

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    public async Task Anonymous_state_changing_auth_call_without_an_antiforgery_token_is_400(string path)
    {
        var client = NewClient();

        using var response = await client.PostAsync(
            path,
            new { accountType = "company", name = "Acme", email = NewEmail(), password = Password },
            csrfToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Authenticated_logout_without_an_antiforgery_token_is_400_and_leaves_the_session_intact()
    {
        var client = NewClient();
        var email = NewEmail();
        using (var reg = await client.RegisterAsync("Acme Inc.", email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        }

        using var response = await client.PostAsync("/api/auth/logout", new { }, csrfToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);

        // No state change: the caller is still signed in.
        using var me = await client.Http.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    // ---- Row: structured logs never carry secrets --------------

    [Fact]
    public async Task No_cookie_token_email_password_or_hash_value_appears_in_the_structured_logs()
    {
        var client = NewClient();
        var email = NewEmail();

        using (var reg = await client.RegisterAsync("Acme Inc.", email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        }

        var stored = await Db.GetCompanyAccountAsync(email);
        var token = await client.GetCsrfTokenAsync();
        using (await client.PostAsync("/api/auth/logout", new { }, token))
        {
        }

        using (var login = await client.LoginAsync(email, Password))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        foreach (var line in fixture.Logs.Snapshot())
        {
            Assert.DoesNotContain(email, line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Password, line, StringComparison.Ordinal);
            Assert.DoesNotContain(stored!.Value.PasswordHash, line, StringComparison.Ordinal);
            Assert.DoesNotContain("Set-Cookie", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("X-CSRF-TOKEN", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- Row: Host starts with the database reachable ---------

    [Fact]
    public async Task Host_started_with_a_reachable_database_applied_both_migrations_and_health_is_200()
    {
        using var client = fixture.CreateClient();

        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("\"database\":\"ok\"", await health.Content.ReadAsStringAsync());

        // Both migration histories exist (proves both contexts migrated on boot).
        Assert.True(await MigrationApplied("identity", "InitialIdentity"));
        Assert.True(await MigrationApplied("public", "InitialDataProtectionKeys"));

        // Issuing a cookie persists a Data-Protection key row in the public schema.
        using (await NewClient().RegisterAsync("Acme Inc.", NewEmail(), Password))
        {
        }

        Assert.True(await Db.CountDataProtectionKeysAsync() >= 1);
    }

    // ---- Row: PasswordHasher default (config absent) ---------

    [Fact]
    public void PasswordHasher_default_iteration_count_is_at_least_600000_when_configuration_is_absent()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=127.0.0.1;Port=1;Database=nexusjob;Username=probe;Password=probe"));

        var options = factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.PasswordHasherOptions>>()
            .Value;

        Assert.True(options.IterationCount >= 600_000);
    }

    private async Task<bool> MigrationApplied(string schema, string migrationSuffix)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            $"SELECT count(*) FROM \"{schema}\".\"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE @suffix", connection);
        command.Parameters.AddWithValue("suffix", "%" + migrationSuffix);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1;
    }

    private static IReadOnlyList<string> AuthCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(v => v.StartsWith("nexusjob_auth=", StringComparison.Ordinal)).ToArray()
            : [];

    private static bool IsBase64(string value)
    {
        Span<byte> buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out _);
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", mediaType);
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

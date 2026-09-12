using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace NexusJob.IntegrationTests;

/// <summary>The <c>{ id, accountType, displayName }</c> summary returned by register / login / me.</summary>
internal sealed record AuthAccountDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("accountType")] string AccountType,
    [property: JsonPropertyName("displayName")] string DisplayName);

internal sealed record CsrfTokenDto([property: JsonPropertyName("token")] string Token);

/// <summary>The <c>{ id, title, description, createdAt }</c> representation returned by <c>POST /api/job-postings</c>.</summary>
internal sealed record JobPostingDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

/// <summary>The <c>{ id, title, description, companyName }</c> representation returned by <c>GET /api/job-postings/{id}</c>.</summary>
internal sealed record JobPostingDetailDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("companyName")] string CompanyName);

/// <summary>The <c>{ items, page, pageSize, total }</c> shape (AD-15) returned by any paginated-list endpoint.</summary>
internal sealed record PageDto<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("total")] int Total);

/// <summary>One row of <c>GET /api/job-postings</c>'s search results: <c>{ id, title, description, companyName }</c>.</summary>
internal sealed record JobPostingSearchResultDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("companyName")] string CompanyName);

/// <summary>The observed row shape of <c>job_postings.job_posting</c>.</summary>
internal sealed record JobPostingRow(
    Guid Id,
    Guid OwnerCompanyId,
    string Title,
    string Description,
    DateTimeOffset CreatedAt);

/// <summary>The <c>{ id, jobPostingId, submittedAt }</c> representation returned by <c>POST /api/applications</c>.</summary>
internal sealed record ApplicationDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("jobPostingId")] string JobPostingId,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt);

/// <summary>The <c>{ applied, appliedAt }</c> shape returned by <c>GET /api/applications/mine</c>.</summary>
internal sealed record MyApplicationDto(
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("appliedAt")] DateTimeOffset? AppliedAt);

/// <summary>One row of <c>GET /api/applications/mine/list</c>'s paged results: <c>{ applicationId, jobPostingId, jobPostingTitle, submittedAt }</c>.</summary>
internal sealed record MyApplicationListItemDto(
    [property: JsonPropertyName("applicationId")] string ApplicationId,
    [property: JsonPropertyName("jobPostingId")] string JobPostingId,
    [property: JsonPropertyName("jobPostingTitle")] string JobPostingTitle,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt);

/// <summary>One row of <c>GET /api/job-postings/mine</c>'s paged results: <c>{ id, title, description, createdAt }</c>.</summary>
internal sealed record JobPostingMineItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

/// <summary>One row of <c>GET /api/applications</c>'s paged applicant results: <c>{ jobSeekerId, fullName, email, submittedAt }</c>.</summary>
internal sealed record ApplicantListItemDto(
    [property: JsonPropertyName("jobSeekerId")] string JobSeekerId,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt);

/// <summary>The observed shape of <c>identity.job_seeker_account</c> after the migration.</summary>
internal sealed record JobSeekerAccountSchema(
    IReadOnlyList<(string Name, string DataType, bool NotNull)> Columns,
    bool HasUniqueEmailIndex,
    bool IdIsPrimaryKey);

/// <summary>Direct read-only SQL against the Testcontainer, so assertions on rows never go through a module type.</summary>
internal sealed class IdentityDatabase(string connectionString)
{
    public async Task<int> CountCompanyAccountsAsync(string email)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM identity.company_account WHERE email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<(string Email, string PasswordHash, string DisplayName)?> GetCompanyAccountAsync(string email)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT email, password_hash, display_name FROM identity.company_account WHERE email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task<int> CountJobSeekerAccountsAsync(string email)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM identity.job_seeker_account WHERE email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<(string Email, string PasswordHash, string FullName)?> GetJobSeekerAccountAsync(string email)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT email, password_hash, full_name FROM identity.job_seeker_account WHERE email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    /// <summary>
    /// Probes <c>information_schema</c> for the shape of <c>identity.job_seeker_account</c>:
    /// each column's name + nullability, plus whether a unique index covers exactly
    /// <c>email</c>. Lets a test assert the migration's result without EF types.
    /// </summary>
    public async Task<JobSeekerAccountSchema?> GetJobSeekerAccountSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var columns = new List<(string Name, string DataType, bool NotNull)>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'job_seeker_account'
            ORDER BY column_name
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1),
                    string.Equals(reader.GetString(2), "NO", StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (columns.Count == 0)
        {
            return null;
        }

        bool hasUniqueEmailIndex;
        await using (var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (i.indkey)
            WHERE n.nspname = 'identity' AND t.relname = 'job_seeker_account'
              AND i.indisunique AND i.indnatts = 1 AND a.attname = 'email'
            """, connection))
        {
            hasUniqueEmailIndex = Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1;
        }

        bool idIsPrimaryKey;
        await using (var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = 'identity' AND tc.table_name = 'job_seeker_account'
              AND tc.constraint_type = 'PRIMARY KEY' AND kcu.column_name = 'id'
            """, connection))
        {
            idIsPrimaryKey = Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1;
        }

        return new JobSeekerAccountSchema(columns, hasUniqueEmailIndex, idIsPrimaryKey);
    }

    public async Task<int> CountDataProtectionKeysAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM public.data_protection_keys", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}

/// <summary>
/// Direct read-only SQL against the Testcontainer for the <c>job_postings</c>
/// schema, so assertions on rows never go through a JobPostings module type
/// (which is <c>internal</c> and unreferenced anyway).
/// </summary>
internal sealed class JobPostingsDatabase(string connectionString)
{
    public async Task<int> CountPostingsForOwnerAsync(Guid ownerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM job_postings.job_posting WHERE owner_company_id = @owner", connection);
        command.Parameters.AddWithValue("owner", ownerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> CountAllPostingsAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM job_postings.job_posting", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<JobPostingRow?> GetPostingAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id, owner_company_id, title, description, created_at FROM job_postings.job_posting WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new JobPostingRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4));
    }

    /// <summary>
    /// Inserts a posting row directly, bypassing <c>POST /api/job-postings</c>, so a
    /// test can construct an orphan posting whose <c>owner_company_id</c> has no
    /// <c>company_account</c> (unreachable through the API - every real posting's
    /// owner comes from an authenticated Company). Returns the new posting id.
    /// </summary>
    public async Task<Guid> InsertPostingAsync(Guid ownerCompanyId, string title, string description)
    {
        var id = Guid.CreateVersion7();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO job_postings.job_posting (id, owner_company_id, title, description, created_at) " +
            "VALUES (@id, @owner, @title, @description, @createdAt)",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("owner", ownerCompanyId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("description", description);
        command.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync();
        return id;
    }
}

/// <summary>
/// Direct read-only SQL against the Testcontainer for the <c>applications</c>
/// schema, so assertions on rows never go through an Applications module type
/// (which is <c>internal</c> and unreferenced anyway).
/// </summary>
internal sealed class ApplicationsDatabase(string connectionString)
{
    public async Task<int> CountApplicationsForPostingAsync(Guid postingId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM applications.application WHERE job_posting_id = @posting", connection);
        command.Parameters.AddWithValue("posting", postingId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> CountApplicationsForSeekerAsync(Guid seekerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM applications.application WHERE job_seeker_id = @seeker", connection);
        command.Parameters.AddWithValue("seeker", seekerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<(Guid Id, DateTimeOffset SubmittedAt)?> GetApplicationRowAsync(Guid postingId, Guid seekerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id, submitted_at FROM applications.application " +
            "WHERE job_posting_id = @posting AND job_seeker_id = @seeker", connection);
        command.Parameters.AddWithValue("posting", postingId);
        command.Parameters.AddWithValue("seeker", seekerId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    /// <summary>
    /// Probes <c>information_schema</c> for whether the observed columns of
    /// <c>applications.application</c> match the four expected ones (name +
    /// nullability), plus whether a unique index covers exactly
    /// <c>(job_posting_id, job_seeker_id)</c> in that order.
    /// </summary>
    public async Task<(IReadOnlyList<(string Name, string DataType, bool NotNull)> Columns, bool HasUniquePairIndex)> GetApplicationSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var columns = new List<(string Name, string DataType, bool NotNull)>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'applications' AND table_name = 'application'
            ORDER BY column_name
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1),
                    string.Equals(reader.GetString(2), "NO", StringComparison.OrdinalIgnoreCase)));
            }
        }

        bool hasUniquePairIndex;
        await using (var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a1 ON a1.attrelid = t.oid AND a1.attnum = i.indkey[0]
            JOIN pg_attribute a2 ON a2.attrelid = t.oid AND a2.attnum = i.indkey[1]
            WHERE n.nspname = 'applications' AND t.relname = 'application'
              AND i.indisunique AND i.indnatts = 2
              AND a1.attname = 'job_posting_id' AND a2.attname = 'job_seeker_id'
            """, connection))
        {
            hasUniquePairIndex = Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1;
        }

        return (columns, hasUniquePairIndex);
    }

    /// <summary>
    /// Inserts an application row directly, bypassing <c>POST /api/applications</c>,
    /// so a test can control row order/count for pagination assertions and seed an
    /// orphan <paramref name="jobPostingId"/> that matches no real posting (a
    /// data-integrity violation, unreachable through the API - mirrors
    /// <c>JobPostingsDatabase.InsertPostingAsync</c>'s orphan-row seeding pattern).
    /// Returns the new application id (a <see cref="Guid"/> v7, generated here).
    /// </summary>
    public async Task<Guid> InsertApplicationAsync(Guid jobPostingId, Guid jobSeekerId, DateTimeOffset submittedAt)
    {
        var id = Guid.CreateVersion7();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO applications.application (id, job_posting_id, job_seeker_id, submitted_at) " +
            "VALUES (@id, @jobPostingId, @jobSeekerId, @submittedAt)",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("jobPostingId", jobPostingId);
        command.Parameters.AddWithValue("jobSeekerId", jobSeekerId);
        command.Parameters.AddWithValue("submittedAt", submittedAt);
        await command.ExecuteNonQueryAsync();
        return id;
    }
}

/// <summary>
/// Wraps an <see cref="HttpClient"/> with the antiforgery double-submit dance:
/// seed the token from <c>GET /api/auth/csrf</c> (which also sets the antiforgery
/// cookie in this client's cookie container) and send it back in
/// <c>X-CSRF-TOKEN</c> on state-changing calls.
/// </summary>
internal sealed class AuthApiClient(HttpClient http)
{
    public HttpClient Http => http;

    public async Task<string> GetCsrfTokenAsync()
    {
        using var response = await http.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfTokenDto>();
        return body!.Token;
    }

    public async Task<HttpResponseMessage> PostAsync(string path, object body, string? csrfToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        if (csrfToken is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        }

        return await http.SendAsync(request);
    }

    public async Task<HttpResponseMessage> RegisterAsync(string name, string email, string password, string accountType = "company")
    {
        var token = await GetCsrfTokenAsync();
        return await PostAsync("/api/auth/register", new { accountType, name, email, password }, token);
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password, string accountType = "company")
    {
        var token = await GetCsrfTokenAsync();
        return await PostAsync("/api/auth/login", new { accountType, email, password }, token);
    }

    /// <summary>
    /// <c>POST /api/job-postings</c> with a freshly seeded antiforgery token (the
    /// same seeding <see cref="RegisterAsync"/> does). Use the
    /// <paramref name="csrfToken"/> overload to send a specific token or
    /// <c>null</c> (the missing-token I/O-matrix row).
    /// </summary>
    public async Task<HttpResponseMessage> CreatePostingAsync(string title, string description)
    {
        var token = await GetCsrfTokenAsync();
        return await CreatePostingAsync(title, description, token);
    }

    public Task<HttpResponseMessage> CreatePostingAsync(string title, string description, string? csrfToken) =>
        PostAsync("/api/job-postings", new { title, description }, csrfToken);

    /// <summary>
    /// <c>GET /api/job-postings/{id}</c> - anonymous (AD-18): no antiforgery seed,
    /// no auth header. The client's cookie container is used as-is, so a caller
    /// that has already signed in exercises the "authenticated caller gets the
    /// identical 200" row.
    /// </summary>
    public Task<HttpResponseMessage> GetPostingAsync(Guid id) =>
        http.GetAsync($"/api/job-postings/{id}");

    /// <summary>Same, but with a raw (possibly non-GUID) path segment for the routing-404 row.</summary>
    public Task<HttpResponseMessage> GetPostingRawAsync(string idSegment) =>
        http.GetAsync($"/api/job-postings/{idSegment}");

    /// <summary>
    /// <c>GET /api/job-postings?query=&amp;page=&amp;pageSize=</c> - anonymous
    /// (AD-18): no antiforgery seed, no auth header. Every parameter is
    /// optional; a <see langword="null"/> argument omits that query-string entry.
    /// </summary>
    public Task<HttpResponseMessage> SearchPostingsAsync(string? query = null, int? page = null, int? pageSize = null)
    {
        var parameters = new List<string>();
        if (query is not null)
        {
            parameters.Add($"query={Uri.EscapeDataString(query)}");
        }

        if (page is not null)
        {
            parameters.Add($"page={page.Value}");
        }

        if (pageSize is not null)
        {
            parameters.Add($"pageSize={pageSize.Value}");
        }

        var queryString = parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
        return http.GetAsync($"/api/job-postings{queryString}");
    }

    /// <summary>
    /// <c>POST /api/applications</c> with a freshly seeded antiforgery token (the
    /// same seeding <see cref="RegisterAsync"/> does). Use the
    /// <paramref name="csrfToken"/> overload to send a specific token or
    /// <c>null</c> (the missing-token I/O-matrix row).
    /// </summary>
    public async Task<HttpResponseMessage> ApplyAsync(string jobPostingId)
    {
        var token = await GetCsrfTokenAsync();
        return await ApplyAsync(jobPostingId, token);
    }

    public Task<HttpResponseMessage> ApplyAsync(string jobPostingId, string? csrfToken) =>
        PostAsync("/api/applications", new { jobPostingId }, csrfToken);

    /// <summary>
    /// <c>GET /api/applications/mine?jobPostingId=</c> - a Job Seeker read (AD-20):
    /// no antiforgery seed (GET). The client's cookie container is used as-is. A
    /// <see langword="null"/> <paramref name="jobPostingId"/> omits the query-string
    /// entry (the missing-param I/O-matrix row).
    /// </summary>
    public Task<HttpResponseMessage> GetMyApplicationAsync(string? jobPostingId)
    {
        var queryString = jobPostingId is null
            ? string.Empty
            : $"?jobPostingId={Uri.EscapeDataString(jobPostingId)}";
        return http.GetAsync($"/api/applications/mine{queryString}");
    }

    /// <summary>
    /// <c>GET /api/applications/mine/list?page=&amp;pageSize=</c> - a Job Seeker
    /// read (story 3.3a): no antiforgery seed (GET). The client's cookie
    /// container is used as-is. Every parameter is optional; a
    /// <see langword="null"/> argument omits that query-string entry (mirrors
    /// <see cref="SearchPostingsAsync"/>).
    /// </summary>
    public Task<HttpResponseMessage> GetMyApplicationsAsync(int? page = null, int? pageSize = null)
    {
        var parameters = new List<string>();
        if (page is not null)
        {
            parameters.Add($"page={page.Value}");
        }

        if (pageSize is not null)
        {
            parameters.Add($"pageSize={pageSize.Value}");
        }

        var queryString = parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
        return http.GetAsync($"/api/applications/mine/list{queryString}");
    }

    /// <summary>
    /// <c>GET /api/job-postings/mine?page=&amp;pageSize=</c> - a Company read (spec
    /// 3.4a): no antiforgery seed (GET). The client's cookie container is used
    /// as-is. Every parameter is optional; a <see langword="null"/> argument
    /// omits that query-string entry (mirrors <see cref="SearchPostingsAsync"/>).
    /// </summary>
    public Task<HttpResponseMessage> GetMyPostingsAsync(int? page = null, int? pageSize = null)
    {
        var parameters = new List<string>();
        if (page is not null)
        {
            parameters.Add($"page={page.Value}");
        }

        if (pageSize is not null)
        {
            parameters.Add($"pageSize={pageSize.Value}");
        }

        var queryString = parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
        return http.GetAsync($"/api/job-postings/mine{queryString}");
    }

    /// <summary>
    /// <c>GET /api/applications?jobPostingId=&amp;page=&amp;pageSize=</c> - a
    /// Company read (spec 3.4a): no antiforgery seed (GET). The client's cookie
    /// container is used as-is. A <see langword="null"/> <paramref name="jobPostingId"/>
    /// omits the query-string entry (the missing-param I/O-matrix row); <c>page</c>
    /// / <c>pageSize</c> are optional (mirrors <see cref="SearchPostingsAsync"/>).
    /// </summary>
    public Task<HttpResponseMessage> GetApplicantsAsync(string? jobPostingId, int? page = null, int? pageSize = null)
    {
        var parameters = new List<string>();
        if (jobPostingId is not null)
        {
            parameters.Add($"jobPostingId={Uri.EscapeDataString(jobPostingId)}");
        }

        if (page is not null)
        {
            parameters.Add($"page={page.Value}");
        }

        if (pageSize is not null)
        {
            parameters.Add($"pageSize={pageSize.Value}");
        }

        var queryString = parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
        return http.GetAsync($"/api/applications{queryString}");
    }
}

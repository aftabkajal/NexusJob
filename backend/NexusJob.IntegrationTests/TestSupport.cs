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
}

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

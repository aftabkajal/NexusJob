using System.Net;
using System.Text.Json;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// Pins the served OpenAPI document (spec 1.3b-i / AD-15): <c>GET /openapi/v1.json</c>
/// returns a <c>200 application/json</c> body whose top-level <c>openapi</c> is a
/// <c>3.0.x</c> version and whose <c>paths</c> describe the five <c>/api/auth/*</c>
/// operations - each with its expected operation id and response status codes, so
/// the generated TypeScript client's method names and error handling stay sane.
/// Reuses the shared <see cref="IdentityApiFixture"/> (no new fixture); the
/// endpoint is anonymous, so no Postgres row or cookie is needed.
/// </summary>
[Collection(nameof(IdentityApiCollection))]
public sealed class OpenApiDocumentTests(IdentityApiFixture fixture)
{
    /// <summary>path -&gt; (HTTP method, operationId, expected response status codes).</summary>
    private static readonly (string Path, string Method, string OperationId, string[] StatusCodes)[] ExpectedAuthOperations =
    [
        ("/api/auth/register", "post", "Auth_Register", ["200", "400", "409"]),
        ("/api/auth/login", "post", "Auth_Login", ["200", "400", "401"]),
        ("/api/auth/me", "get", "Auth_Me", ["200", "401"]),
        ("/api/auth/logout", "post", "Auth_Logout", ["204", "400", "401"]),
        ("/api/auth/csrf", "get", "Auth_Csrf", ["200"]),
    ];

    [Fact]
    public async Task Document_is_openapi_3_0_and_describes_the_five_auth_operations()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(
            root.TryGetProperty("openapi", out var openApiVersionElement),
            "OpenAPI document has no top-level 'openapi' property.");
        var openApiVersion = openApiVersionElement.GetString();
        Assert.NotNull(openApiVersion);
        Assert.StartsWith("3.0", openApiVersion);

        Assert.True(
            root.TryGetProperty("paths", out var paths),
            "OpenAPI document has no 'paths' object.");

        foreach (var (path, method, operationId, statusCodes) in ExpectedAuthOperations)
        {
            Assert.True(
                paths.TryGetProperty(path, out var pathItem),
                $"OpenAPI document is missing the '{path}' path.");

            Assert.True(
                pathItem.TryGetProperty(method, out var operation),
                $"'{path}' does not document a '{method.ToUpperInvariant()}' operation.");

            Assert.True(
                operation.TryGetProperty("operationId", out var operationIdElement),
                $"'{method.ToUpperInvariant()} {path}' has no operationId.");
            Assert.Equal(operationId, operationIdElement.GetString());

            Assert.True(
                operation.TryGetProperty("responses", out var responses),
                $"'{method.ToUpperInvariant()} {path}' documents no responses.");
            foreach (var statusCode in statusCodes)
            {
                Assert.True(
                    responses.TryGetProperty(statusCode, out _),
                    $"'{method.ToUpperInvariant()} {path}' does not document a '{statusCode}' response.");
            }
        }
    }
}

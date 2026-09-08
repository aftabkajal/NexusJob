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

    // ---- Row: Fetch the OpenAPI document -- POST /api/job-postings (spec 2.1a) --

    [Fact]
    public async Task Document_describes_the_job_postings_create_operation_with_its_200_shape_and_problem_responses()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/job-postings", out var pathItem),
            "OpenAPI document is missing the '/api/job-postings' path.");
        Assert.True(
            pathItem.TryGetProperty("post", out var operation),
            "'/api/job-postings' does not document a 'POST' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("JobPostings_Create", operationId.GetString());

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "400", "401", "403" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'POST /api/job-postings' does not document a '{statusCode}' response.");
        }

        // The 200 body is the { id, title, description, createdAt } representation.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schemaProperties = ResolveSchemaProperties(root, okSchema);
        Assert.Equal(
            ["createdAt", "description", "id", "title"],
            schemaProperties.OrderBy(name => name, StringComparer.Ordinal));

        // 4xx responses are RFC 9457 ProblemDetails.
        foreach (var statusCode in new[] { "400", "401", "403" })
        {
            Assert.True(
                responses.GetProperty(statusCode).GetProperty("content")
                    .TryGetProperty("application/problem+json", out _),
                $"'POST /api/job-postings' {statusCode} is not a problem+json response.");
        }
    }

    // ---- Row: Fetch the OpenAPI document -- GET /api/job-postings/{id} (spec 2.2a) --

    [Fact]
    public async Task Document_describes_the_job_postings_get_by_id_operation_with_its_200_shape_and_404()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/job-postings/{id}", out var pathItem),
            "OpenAPI document is missing the '/api/job-postings/{id}' path.");
        Assert.True(
            pathItem.TryGetProperty("get", out var operation),
            "'/api/job-postings/{id}' does not document a 'GET' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("JobPostings_GetById", operationId.GetString());

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "404" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'GET /api/job-postings/{{id}}' does not document a '{statusCode}' response.");
        }

        // The 200 body is the { id, title, description, companyName } representation.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schemaProperties = ResolveSchemaProperties(root, okSchema);
        Assert.Equal(
            ["companyName", "description", "id", "title"],
            schemaProperties.OrderBy(name => name, StringComparer.Ordinal));

        // 404 is RFC 9457 ProblemDetails.
        Assert.True(
            responses.GetProperty("404").GetProperty("content")
                .TryGetProperty("application/problem+json", out _),
            "'GET /api/job-postings/{id}' 404 is not a problem+json response.");
    }

    private static IReadOnlyList<string> ResolveSchemaProperties(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/')[^1];
            schema = root.GetProperty("components").GetProperty("schemas").GetProperty(name);
        }

        return schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();
    }
}

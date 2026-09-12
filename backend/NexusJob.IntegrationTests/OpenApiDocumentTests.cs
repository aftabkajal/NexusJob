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

    // ---- Row: Fetch the OpenAPI document -- GET /api/job-postings (spec 2.3a) --

    [Fact]
    public async Task Document_describes_the_job_postings_search_operation_with_its_query_params_and_200_shape()
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
            pathItem.TryGetProperty("get", out var operation),
            "'/api/job-postings' does not document a 'GET' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("JobPostings_Search", operationId.GetString());

        // query / page / pageSize are all optional query parameters.
        Assert.True(operation.TryGetProperty("parameters", out var parameters));
        foreach (var name in new[] { "query", "page", "pageSize" })
        {
            var parameter = parameters.EnumerateArray()
                .FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == name);
            Assert.True(
                parameter.ValueKind != JsonValueKind.Undefined,
                $"'GET /api/job-postings' does not document a '{name}' parameter.");
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            var isRequired = parameter.TryGetProperty("required", out var requiredElement) && requiredElement.GetBoolean();
            Assert.False(isRequired, $"'{name}' is documented as required; it must be optional.");
        }

        Assert.True(operation.TryGetProperty("responses", out var responses));
        Assert.True(
            responses.TryGetProperty("200", out _),
            "'GET /api/job-postings' does not document a '200' response.");

        // The 200 body is Page<JobPostingSearchResultResponse>: { items[], page, pageSize, total }.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schemaProperties = ResolveSchemaProperties(root, okSchema);
        Assert.Equal(
            ["items", "page", "pageSize", "total"],
            schemaProperties.OrderBy(name => name, StringComparer.Ordinal));

        // Each item in `items` is { id, title, description, companyName }.
        var itemsSchema = ResolveSchema(root, okSchema).GetProperty("properties").GetProperty("items");
        var itemSchema = itemsSchema.GetProperty("items");
        var itemProperties = ResolveSchemaProperties(root, itemSchema);
        Assert.Equal(
            ["companyName", "description", "id", "title"],
            itemProperties.OrderBy(name => name, StringComparer.Ordinal));
    }

    // ---- Row: Fetch the OpenAPI document -- POST /api/applications (spec 3.1a) --

    [Fact]
    public async Task Document_describes_the_applications_create_operation_with_its_200_shape_and_problem_responses()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/applications", out var pathItem),
            "OpenAPI document is missing the '/api/applications' path.");
        Assert.True(
            pathItem.TryGetProperty("post", out var operation),
            "'/api/applications' does not document a 'POST' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("Applications_Create", operationId.GetString());

        // The request body is CreateApplicationRequest { jobPostingId }.
        var requestSchema = operation.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal(["jobPostingId"], ResolveSchemaProperties(root, requestSchema));

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "400", "401", "403", "404" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'POST /api/applications' does not document a '{statusCode}' response.");
        }

        // The 200 body is the { id, jobPostingId, submittedAt } representation.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal(
            ["id", "jobPostingId", "submittedAt"],
            ResolveSchemaProperties(root, okSchema).OrderBy(name => name, StringComparer.Ordinal));

        // 4xx responses are RFC 9457 ProblemDetails (the 404 is the unknown-posting case).
        foreach (var statusCode in new[] { "400", "401", "403", "404" })
        {
            Assert.True(
                responses.GetProperty(statusCode).GetProperty("content")
                    .TryGetProperty("application/problem+json", out _),
                $"'POST /api/applications' {statusCode} is not a problem+json response.");
        }
    }

    // ---- Row: Fetch the OpenAPI document -- GET /api/applications/mine (spec 3.1a) --

    [Fact]
    public async Task Document_describes_the_applications_get_mine_operation_with_its_required_query_param_and_200_shape()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/applications/mine", out var pathItem),
            "OpenAPI document is missing the '/api/applications/mine' path.");
        Assert.True(
            pathItem.TryGetProperty("get", out var operation),
            "'/api/applications/mine' does not document a 'GET' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("Applications_GetMine", operationId.GetString());

        // jobPostingId is a required query parameter.
        Assert.True(operation.TryGetProperty("parameters", out var parameters));
        var jobPostingId = parameters.EnumerateArray()
            .FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == "jobPostingId");
        Assert.True(
            jobPostingId.ValueKind != JsonValueKind.Undefined,
            "'GET /api/applications/mine' does not document a 'jobPostingId' parameter.");
        Assert.Equal("query", jobPostingId.GetProperty("in").GetString());
        Assert.True(
            jobPostingId.TryGetProperty("required", out var requiredElement) && requiredElement.GetBoolean(),
            "'jobPostingId' must be documented as a required query parameter.");

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "400", "401", "403" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'GET /api/applications/mine' does not document a '{statusCode}' response.");
        }

        // The 200 body is MyApplicationResponse { applied, appliedAt }.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal(
            ["applied", "appliedAt"],
            ResolveSchemaProperties(root, okSchema).OrderBy(name => name, StringComparer.Ordinal));
    }

    // ---- Row: Fetch the OpenAPI document -- GET /api/applications/mine/list (spec 3.3a) --

    [Fact]
    public async Task Document_describes_the_my_applications_list_operation_and_leaves_get_mine_unchanged()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/applications/mine/list", out var pathItem),
            "OpenAPI document is missing the '/api/applications/mine/list' path.");
        Assert.True(
            pathItem.TryGetProperty("get", out var operation),
            "'/api/applications/mine/list' does not document a 'GET' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("Applications_GetMyApplications", operationId.GetString());

        // page / pageSize are both optional query parameters.
        Assert.True(operation.TryGetProperty("parameters", out var parameters));
        foreach (var name in new[] { "page", "pageSize" })
        {
            var parameter = parameters.EnumerateArray()
                .FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == name);
            Assert.True(
                parameter.ValueKind != JsonValueKind.Undefined,
                $"'GET /api/applications/mine/list' does not document a '{name}' parameter.");
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            var isRequired = parameter.TryGetProperty("required", out var requiredElement) && requiredElement.GetBoolean();
            Assert.False(isRequired, $"'{name}' is documented as required; it must be optional.");
        }

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "401", "403" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'GET /api/applications/mine/list' does not document a '{statusCode}' response.");
        }

        // The 200 body is Page<MyApplicationListItemResponse>: { items[], page, pageSize, total }.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schemaProperties = ResolveSchemaProperties(root, okSchema);
        Assert.Equal(
            ["items", "page", "pageSize", "total"],
            schemaProperties.OrderBy(name => name, StringComparer.Ordinal));

        // Each item in `items` is { applicationId, jobPostingId, jobPostingTitle, submittedAt }.
        var itemsSchema = ResolveSchema(root, okSchema).GetProperty("properties").GetProperty("items");
        var itemSchema = itemsSchema.GetProperty("items");
        var itemProperties = ResolveSchemaProperties(root, itemSchema);
        Assert.Equal(
            ["applicationId", "jobPostingId", "jobPostingTitle", "submittedAt"],
            itemProperties.OrderBy(name => name, StringComparer.Ordinal));

        // The existing /mine probe's operation is untouched by this story.
        Assert.True(
            paths.TryGetProperty("/api/applications/mine", out var minePathItem),
            "OpenAPI document is missing the '/api/applications/mine' path.");
        Assert.True(
            minePathItem.TryGetProperty("get", out var mineOperation),
            "'/api/applications/mine' does not document a 'GET' operation.");
        Assert.True(mineOperation.TryGetProperty("operationId", out var mineOperationId));
        Assert.Equal("Applications_GetMine", mineOperationId.GetString());
    }

    // ---- Row: Fetch the OpenAPI document -- GET /api/job-postings/mine (spec 3.4a) --

    [Fact]
    public async Task Document_describes_the_job_postings_get_mine_operation_with_its_query_params_and_200_shape()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/job-postings/mine", out var pathItem),
            "OpenAPI document is missing the '/api/job-postings/mine' path.");
        Assert.True(
            pathItem.TryGetProperty("get", out var operation),
            "'/api/job-postings/mine' does not document a 'GET' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("JobPostings_GetMine", operationId.GetString());

        // page / pageSize are both optional query parameters.
        Assert.True(operation.TryGetProperty("parameters", out var parameters));
        foreach (var name in new[] { "page", "pageSize" })
        {
            var parameter = parameters.EnumerateArray()
                .FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == name);
            Assert.True(
                parameter.ValueKind != JsonValueKind.Undefined,
                $"'GET /api/job-postings/mine' does not document a '{name}' parameter.");
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            var isRequired = parameter.TryGetProperty("required", out var requiredElement) && requiredElement.GetBoolean();
            Assert.False(isRequired, $"'{name}' is documented as required; it must be optional.");
        }

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "401", "403" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'GET /api/job-postings/mine' does not document a '{statusCode}' response.");
        }

        // The 200 body is Page<JobPostingMineItemResponse>: { items[], page, pageSize, total }.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schemaProperties = ResolveSchemaProperties(root, okSchema);
        Assert.Equal(
            ["items", "page", "pageSize", "total"],
            schemaProperties.OrderBy(name => name, StringComparer.Ordinal));

        // Each item in `items` is { id, title, description, createdAt } - no companyName, no applicant count.
        var itemsSchema = ResolveSchema(root, okSchema).GetProperty("properties").GetProperty("items");
        var itemSchema = itemsSchema.GetProperty("items");
        var itemProperties = ResolveSchemaProperties(root, itemSchema);
        Assert.Equal(
            ["createdAt", "description", "id", "title"],
            itemProperties.OrderBy(name => name, StringComparer.Ordinal));

        // The existing search/create/getById operations are untouched by this story.
        Assert.True(paths.TryGetProperty("/api/job-postings", out var searchPathItem));
        Assert.True(searchPathItem.TryGetProperty("get", out var searchOperation));
        Assert.True(searchOperation.TryGetProperty("operationId", out var searchOperationId));
        Assert.Equal("JobPostings_Search", searchOperationId.GetString());
    }

    // ---- Row: Fetch the OpenAPI document -- GET /api/applications (spec 3.4a) --

    [Fact]
    public async Task Document_describes_the_applications_get_applicants_operation_with_its_query_params_and_200_shape()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(
            paths.TryGetProperty("/api/applications", out var pathItem),
            "OpenAPI document is missing the '/api/applications' path.");
        Assert.True(
            pathItem.TryGetProperty("get", out var operation),
            "'/api/applications' does not document a 'GET' operation.");

        Assert.True(operation.TryGetProperty("operationId", out var operationId));
        Assert.Equal("Applications_GetApplicants", operationId.GetString());

        // jobPostingId is a required query parameter; page / pageSize are optional.
        Assert.True(operation.TryGetProperty("parameters", out var parameters));
        var jobPostingId = parameters.EnumerateArray()
            .FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == "jobPostingId");
        Assert.True(
            jobPostingId.ValueKind != JsonValueKind.Undefined,
            "'GET /api/applications' does not document a 'jobPostingId' parameter.");
        Assert.Equal("query", jobPostingId.GetProperty("in").GetString());
        Assert.True(
            jobPostingId.TryGetProperty("required", out var requiredElement) && requiredElement.GetBoolean(),
            "'jobPostingId' must be documented as a required query parameter.");

        foreach (var name in new[] { "page", "pageSize" })
        {
            var parameter = parameters.EnumerateArray()
                .FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == name);
            Assert.True(
                parameter.ValueKind != JsonValueKind.Undefined,
                $"'GET /api/applications' does not document a '{name}' parameter.");
            var isRequired = parameter.TryGetProperty("required", out var paramRequiredElement) && paramRequiredElement.GetBoolean();
            Assert.False(isRequired, $"'{name}' is documented as required; it must be optional.");
        }

        Assert.True(operation.TryGetProperty("responses", out var responses));
        foreach (var statusCode in new[] { "200", "400", "401", "403", "404" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"'GET /api/applications' does not document a '{statusCode}' response.");
        }

        // The 200 body is Page<ApplicantListItemResponse>: { items[], page, pageSize, total }.
        var okSchema = responses.GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schemaProperties = ResolveSchemaProperties(root, okSchema);
        Assert.Equal(
            ["items", "page", "pageSize", "total"],
            schemaProperties.OrderBy(name => name, StringComparer.Ordinal));

        // Each item in `items` is { jobSeekerId, fullName, email, submittedAt } - no posting fields.
        var itemsSchema = ResolveSchema(root, okSchema).GetProperty("properties").GetProperty("items");
        var itemSchema = itemsSchema.GetProperty("items");
        var itemProperties = ResolveSchemaProperties(root, itemSchema);
        Assert.Equal(
            ["email", "fullName", "jobSeekerId", "submittedAt"],
            itemProperties.OrderBy(name => name, StringComparer.Ordinal));

        // 4xx responses are RFC 9457 ProblemDetails.
        foreach (var statusCode in new[] { "400", "401", "403", "404" })
        {
            Assert.True(
                responses.GetProperty(statusCode).GetProperty("content")
                    .TryGetProperty("application/problem+json", out _),
                $"'GET /api/applications' {statusCode} is not a problem+json response.");
        }

        // The existing POST /api/applications create operation is untouched by this story.
        Assert.True(pathItem.TryGetProperty("post", out var createOperation));
        Assert.True(createOperation.TryGetProperty("operationId", out var createOperationId));
        Assert.Equal("Applications_Create", createOperationId.GetString());
    }

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/')[^1];
            return root.GetProperty("components").GetProperty("schemas").GetProperty(name);
        }

        return schema;
    }

    private static IReadOnlyList<string> ResolveSchemaProperties(JsonElement root, JsonElement schema) =>
        ResolveSchema(root, schema).GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cube.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cube.Tests;

public sealed class ContractTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Openapi_matches_the_pre_refactoring_contract()
    {
        using var client = fixture.CreateClient();
        var actual = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!.AsObject();
        actual.Remove("servers"); // The host/port is supplied by the deployment, not the API contract.
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Baselines", "openapi.json")));
        Assert.True(JsonNode.DeepEquals(expected, actual), "OpenAPI changed. Review the contract diff; do not regenerate the baseline to hide a regression.");
    }

    [Fact]
    public void Ef_model_matches_the_existing_migration_snapshot()
    {
        using var scope = fixture.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<CubeDb>().Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Composition_resolves_with_scope_and_dependency_validation_enabled()
    {
        using var validated = fixture.WithWebHostBuilder(builder => builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        }));
        using var client = validated.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Theory]
    [InlineData("FullName", "missing_fullname")]
    [InlineData("Inn", "missing_inn")]
    [InlineData("Password", "missing_password")]
    public async Task Required_null_inputs_keep_their_problem_details(string field, string code)
    {
        var input = new Dictionary<string, object?>
        {
            ["Login"] = "contract-applicant", ["Password"] = ApiFixture.Password,
            ["Email"] = "contract@example.test", ["FullName"] = "Applicant",
            ["Organization"] = "Organization", ["Inn"] = "7707083893", ["Phone"] = "1"
        };
        input[field] = null;
        using var client = fixture.CreateClient();
        await AssertProblem(await client.PostAsJsonAsync("/api/v1/auth/register", input), HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Oversized_strings_are_rejected_before_business_validation()
    {
        using var tenant = await fixture.As("tenant-a");
        await AssertProblem(await tenant.PostAsJsonAsync("/api/v1/requests", new
        {
            CategoryId = Guid.NewGuid(), Subject = new string('x', 10001), Description = "Description",
            ContactName = "Contact", Phone = "1", Email = "a@example.test"
        }), HttpStatusCode.BadRequest, "field_too_long");
    }

    [Fact]
    public async Task Oversized_collections_keep_their_validation_error()
    {
        using var admin = await fixture.As("admin");
        await AssertProblem(await admin.PostAsJsonAsync("/api/v1/admin/news", new
        {
            Title = "Title", Summary = "Summary", Body = "Body", Category = "General", Importance = "Normal",
            AllTenants = false, TenantIds = Enumerable.Repeat(Guid.NewGuid(), 1001).ToArray(), PremisesIds = Array.Empty<Guid>()
        }), HttpStatusCode.BadRequest, "collection_too_large");
    }

    [Fact]
    public async Task Request_creation_preserves_body_location_headers_and_atomic_side_effects()
    {
        using var tenant = await fixture.As("tenant-a");
        var response = await tenant.PostAsJsonAsync("/api/v1/requests", new
        {
            CategoryId = fixture.Category, PremisesId = fixture.PremisesA, Subject = " Contract issue ",
            Description = "Description", ContactName = "Contact", Phone = "1", Email = "a@example.test"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertFields(body, "id", "createdAt", "tenantId", "categoryId", "premisesId", "assigneeId", "subject", "description", "contactName", "phone", "email", "status", "dueAt", "version");
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/requests/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal("Contract issue", body.GetProperty("subject").GetString());
        Assert.Equal("New", body.GetProperty("status").GetString());
        Assert.Equal(fixture.TenantA, body.GetProperty("tenantId").GetGuid());
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        Assert.True(await db.Outbox.AnyAsync(x => x.ResourceId == id && x.Kind == "RequestCreated" && x.Status == "AwaitingConfiguration"));
        Assert.True(await db.Audit.AnyAsync(x => x.ResourceId == id && x.ActorId == fixture.UserA && x.Action == "Added:ServiceRequest"));
    }

    [Fact]
    public async Task Invalid_request_does_not_save_request_or_outbox()
    {
        using var tenant = await fixture.As("tenant-a");
        var subject = "invalid-" + Guid.NewGuid();
        await AssertProblem(await tenant.PostAsJsonAsync("/api/v1/requests", new
        {
            CategoryId = fixture.Category, Subject = subject, Description = "Description",
            ContactName = "Contact", Phone = "1", Email = "invalid email"
        }), HttpStatusCode.BadRequest, "invalid_email");
        using var scope = fixture.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<CubeDb>().Requests.AnyAsync(x => x.Subject == subject));
    }

    [Fact]
    public async Task Invoice_response_preserves_financial_and_payment_fields()
    {
        using var tenant = await fixture.As("tenant-a");
        var body = await tenant.GetFromJsonAsync<JsonElement>($"/api/v1/invoices/{fixture.InvoiceA}");
        AssertFields(body, "id", "number", "kind", "issuedOn", "dueOn", "purpose", "amount", "paid", "balance", "currency", "fileId", "status", "paymentState", "canPay");
        Assert.Equal(100m, body.GetProperty("balance").GetDecimal());
        Assert.Equal("Unpaid", body.GetProperty("paymentState").GetString());
        Assert.False(body.GetProperty("canPay").GetBoolean());
        tenant.DefaultRequestHeaders.Add("Idempotency-Key", "contract-attempt");
        await AssertProblem(await tenant.PostAsync($"/api/v1/invoices/{fixture.InvoiceA}/payment-attempts", null), HttpStatusCode.ServiceUnavailable, "payment_provider_not_configured");
    }

    [Fact]
    public async Task Resource_errors_preserve_the_existing_scope_behavior()
    {
        using var other = await fixture.As("tenant-b");
        await AssertProblem(await other.GetAsync($"/api/v1/requests/{fixture.RequestA}"), HttpStatusCode.NotFound, "request_not_found");
        using var staff = await fixture.As("staff");
        // EF wraps the tenant-context error during query parameter extraction. Changing this
        // existing 500 to 403 is a separate behavior correction, not part of the refactor.
        Assert.Equal(HttpStatusCode.InternalServerError, (await staff.GetAsync("/api/v1/documents")).StatusCode);
    }

    private static void AssertFields(JsonElement body, params string[] expected) =>
        Assert.Equal(expected.Order(), body.EnumerateObject().Select(x => x.Name).Order());

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(code, body.GetProperty("title").GetString());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("exception", out _));
    }
}

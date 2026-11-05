using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cube.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace Cube.Tests;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string database = "cube_tests_" + Guid.NewGuid().ToString("N");
    public string Connection => new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CUBE_TEST_DATABASE") ?? "Host=127.0.0.1;Port=5432;Username=cube;Database=cube") { Database = database }.ConnectionString;
    private readonly string storage = Path.Combine(Path.GetTempPath(), "cube-tests-" + Guid.NewGuid().ToString("N"));
    public Guid TenantA { get; } = Guid.NewGuid(); public Guid TenantB { get; } = Guid.NewGuid();
    public Guid UserA { get; private set; }
    public Guid UserB { get; private set; }
    public Guid Staff { get; private set; }
    public Guid PremisesA { get; private set; }
    public Guid MeterA { get; private set; }
    public Guid InvoiceA { get; private set; }
    public Guid RequestA { get; private set; }
    public Guid RequestB { get; private set; }
    public Guid Category { get; private set; }
    public Guid NewsA { get; private set; }
    public const string Password = "Test-only-Strong42!";
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Cube"] = Connection, ["Files:Root"] = storage, ["Files:AllowUnscannedDevelopmentUploads"] = "true", ["Logging:LogLevel:Default"] = "Warning", ["RateLimits:AuthPermitLimit"] = "1000" }));
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        await db.Database.MigrateAsync(); await Seed.Categories(db);
        db.Tenants.AddRange(new Tenant { Id = TenantA, Name = "A", Inn = "7707083893" }, new Tenant { Id = TenantB, Name = "B", Inn = "500100732259" }); await db.SaveChangesAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        async Task<Guid> Add(string name, string role, Guid? tenant) { var u = new User { Id = Guid.NewGuid(), UserName = name, Email = name + "@example.test", TenantId = tenant, Approved = true, FullName = name, Role = role }; var r = await users.CreateAsync(u, Password); Assert.True(r.Succeeded, string.Join(',', r.Errors.Select(x => x.Code))); return u.Id; }
        UserA = await Add("tenant-a", "Tenant", TenantA); UserB = await Add("tenant-b", "Tenant", TenantB); Staff = await Add("staff", "Staff", null); await Add("admin", "Admin", null);
        var p = new Premises { Name = "Office A", Address = "Address", Building = "A", Area = 40, MonthlyRent = 100 }; db.Premises.Add(p); PremisesA = p.Id;
        db.Leases.Add(new Lease { TenantId = TenantA, PremisesId = p.Id, Number = "1", SignedOn = new(2026, 1, 1), StartsOn = new(2026, 1, 1), EndsOn = new(2030, 1, 1) });
        var m = new Meter { TenantId = TenantA, PremisesId = p.Id, SerialNumber = "a-1", LastValue = 10 }; db.Meters.Add(m); MeterA = m.Id;
        var invoice = new Invoice { TenantId = TenantA, ExternalId = "test:1", Number = "1", Amount = 100, DueOn = new(2027, 1, 1), IssuedOn = new(2026, 1, 1) }; db.Invoices.Add(invoice); InvoiceA = invoice.Id;
        Category = await db.Categories.Where(x => x.Code == "rent").Select(x => x.Id).SingleAsync();
        var r1 = new ServiceRequest { TenantId = TenantA, CategoryId = Category, AssigneeId = Staff, Subject = "A", Description = "Test" }; var r2 = new ServiceRequest { TenantId = TenantB, CategoryId = Category, Subject = "B" }; db.Requests.AddRange(r1, r2); RequestA = r1.Id; RequestB = r2.Id;
        db.Messages.Add(new RequestMessage { TenantId = TenantA, RequestId = RequestA, AuthorId = Staff, Text = "PRIVATE STAFF NOTE", Internal = true });
        var news = new News { Title = "A only", Summary = "test", Body = "test", TenantIds = [TenantA], PublishAt = DateTimeOffset.UtcNow.AddHours(-1), RequiresAcknowledgement = true }; db.News.Add(news); NewsA = news.Id;
        db.News.Add(new News { Title = "Future", Summary = "test", Body = "test", AllTenants = true, PublishAt = DateTimeOffset.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
    }
    public async Task<HttpClient> As(string login) { var client = CreateClient(); var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { LoginName = login, Password }); response.EnsureSuccessStatusCode(); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString()); return client; }
    async Task IAsyncLifetime.DisposeAsync() { await base.DisposeAsync(); await using var c = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(Connection) { Database = "postgres" }.ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand(); cmd.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)"; await cmd.ExecuteNonQueryAsync(); if (Directory.Exists(storage)) Directory.Delete(storage, true); }
}
public sealed class ApiTests(ApiFixture f) : IClassFixture<ApiFixture>
{
    [Fact] public async Task Health_and_openapi_work() { var c = f.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/ready")).StatusCode); var r = await c.GetAsync("/openapi/v1.json"); r.EnsureSuccessStatusCode(); Assert.Contains("/api/v1/requests", await r.Content.ReadAsStringAsync()); }
    [Fact] public async Task Unauthenticated_and_tenant_admin_access_denied() { Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/v1/requests")).StatusCode); using var a = await f.As("tenant-a"); Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/v1/admin/tenants")).StatusCode); }
    [Fact] public async Task Tenant_cannot_access_another_tenant_resources() { using var b = await f.As("tenant-b"); foreach (var path in new[] { $"/api/v1/requests/{f.RequestA}", $"/api/v1/invoices/{f.InvoiceA}", $"/api/v1/meters/{f.MeterA}/readings", $"/api/v1/news/{f.NewsA}" }) Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode); }
    [Fact] public async Task Internal_messages_hidden_and_staff_assignment_enforced() { using var a = await f.As("tenant-a"); Assert.DoesNotContain("PRIVATE STAFF NOTE", await a.GetStringAsync($"/api/v1/requests/{f.RequestA}")); Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsJsonAsync($"/api/v1/requests/{f.RequestA}/messages", new { Text = "malicious", Internal = true })).StatusCode); using var staff = await f.As("staff"); Assert.Contains("PRIVATE STAFF NOTE", await staff.GetStringAsync($"/api/v1/requests/{f.RequestA}")); Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/requests/{f.RequestB}")).StatusCode); }
    [Fact] public async Task Registration_requires_approval_and_six_categories_are_seeded() { using var c = f.CreateClient(); var login = "new-" + Guid.NewGuid().ToString("N"); var r = await c.PostAsJsonAsync("/api/v1/auth/register", new { Login = login, ApiFixture.Password, Email = login + "@example.test", FullName = "Applicant", Organization = "Company", Inn = "7707083893", Phone = "123" }); Assert.Equal(HttpStatusCode.Accepted, r.StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/v1/auth/login", new { LoginName = login, ApiFixture.Password })).StatusCode); using var a = await f.As("tenant-a"); Assert.Equal(6, (await a.GetFromJsonAsync<JsonElement>("/api/v1/request-categories")).GetArrayLength()); }
    [Fact] public async Task Meter_reading_is_monotonic_and_queued_without_claiming_delivery() { using var a = await f.As("tenant-a"); Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/v1/meters/{f.MeterA}/readings", new { Value = 1 })).StatusCode); var r = await a.PostAsJsonAsync($"/api/v1/meters/{f.MeterA}/readings", new { Value = 20 }); r.EnsureSuccessStatusCode(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); Assert.True(await db.Outbox.AnyAsync(x => x.Kind == "ReadingSubmitted" && x.Status == "AwaitingConfiguration")); }
    [Fact] public async Task Payment_gateway_is_explicitly_disabled_and_does_not_settle() { using var a = await f.As("tenant-a"); a.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await a.PostAsync($"/api/v1/invoices/{f.InvoiceA}/payment-attempts", null)).StatusCode); using var scope = f.Services.CreateScope(); Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<CubeDb>().Invoices.FindAsync(f.InvoiceA))!.Paid); }
    [Fact] public async Task News_read_and_ack_are_separate_and_future_news_hidden() { using var a = await f.As("tenant-a"); Assert.DoesNotContain("Future", await a.GetStringAsync("/api/v1/news")); (await a.PostAsync($"/api/v1/news/{f.NewsA}/read", null)).EnsureSuccessStatusCode(); using (var s = f.Services.CreateScope()) { var r = await s.ServiceProvider.GetRequiredService<CubeDb>().Receipts.SingleAsync(x => x.NewsId == f.NewsA); Assert.NotNull(r.ReadAt); Assert.Null(r.AcknowledgedAt); } (await a.PostAsync($"/api/v1/news/{f.NewsA}/acknowledge", null)).EnsureSuccessStatusCode(); using var scope = f.Services.CreateScope(); Assert.NotNull((await scope.ServiceProvider.GetRequiredService<CubeDb>().Receipts.SingleAsync(x => x.NewsId == f.NewsA)).AcknowledgedAt); }
    [Fact] public async Task Private_upload_cannot_be_downloaded_by_another_tenant() { using var a = await f.As("tenant-a"); using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent("%PDF-1.4\ntest file"u8.ToArray()), "file", "private.pdf"); var r = await a.PostAsync("/api/v1/files", form); r.EnsureSuccessStatusCode(); var id = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/v1/files/{id}")).StatusCode); using var b = await f.As("tenant-b"); Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/files/{id}")).StatusCode); }
    [Fact] public async Task Blocked_tenant_existing_session_stops_immediately() { var tid = Guid.NewGuid(); var uid = Guid.NewGuid(); using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); db.Tenants.Add(new Tenant { Id = tid, Name = "Blocked test", Inn = "1234567890" }); await db.SaveChangesAsync(); var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>(); Assert.True((await users.CreateAsync(new User { Id = uid, UserName = "block-test", Email = "block@example.test", TenantId = tid, Approved = true }, ApiFixture.Password)).Succeeded); } using var c = await f.As("block-test"); using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); (await db.Tenants.FindAsync(tid))!.Blocked = true; await db.SaveChangesAsync(); } Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/auth/me")).StatusCode); }
    [Fact]
    public async Task Ledger_deduplicates_verified_settlement_and_rejects_overpayment()
    {
        using var s = f.Services.CreateScope(); var db = s.ServiceProvider.GetRequiredService<CubeDb>(); var invoice = new Invoice { TenantId = f.TenantA, ExternalId = "ledger:" + Guid.NewGuid(), Number = "ledger", Amount = 100 }; db.Invoices.Add(invoice); await db.SaveChangesAsync(); var ledger = s.ServiceProvider.GetRequiredService<PaymentLedger>(); var id = await ledger.ApplyVerifiedAsync(invoice.Id, "test", "transaction-" + invoice.Id, 40, "RUB", default); Assert.Equal(id, await ledger.ApplyVerifiedAsync(invoice.Id, "test", "transaction-" + invoice.Id, 40, "RUB", default)); Assert.Equal(40, (await db.Invoices.FindAsync(invoice.Id))!.Paid); await Assert.ThrowsAsync<ApiError>(() => ledger.ApplyVerifiedAsync(invoice.Id, "test", "other", 70, "RUB", default)); Assert.Equal(1, await db.Set<PaymentRecord>().CountAsync(x => x.InvoiceId == invoice.Id));
        await ledger.ApplyVerifiedAsync(invoice.Id, "test", "second-" + invoice.Id, 10, "RUB", default);
        Assert.Equal(50, (await db.Invoices.FindAsync(invoice.Id))!.Paid);
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.UserId == f.UserA && x.ResourceId == invoice.Id && x.Kind == "PaymentSettled"));
    }
    [Fact] public async Task Database_rejects_cross_tenant_message_even_without_http() { using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); db.Messages.Add(new RequestMessage { TenantId = f.TenantB, RequestId = f.RequestA, AuthorId = f.UserB, Text = "wrong tenant" }); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); }
    [Fact] public async Task Null_required_fields_return_validation_error() { using var c = f.CreateClient(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/register", new { Login = "test", Password = ApiFixture.Password, Email = "x@example.test", FullName = "Name", Organization = "Org", Inn = (string?)null, Phone = "1" })).StatusCode); }
    [Fact] public async Task Logout_revokes_token() { using var c = await f.As("tenant-b"); (await c.PostAsync("/api/v1/auth/logout", null)).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/auth/me")).StatusCode); }

    [Fact]
    public async Task Approved_application_can_join_only_explicitly_selected_matching_tenant()
    {
        using var admin = await f.As("admin"); using var publicClient = f.CreateClient(); var login = "approve-" + Guid.NewGuid().ToString("N");
        (await publicClient.PostAsJsonAsync("/api/v1/auth/register", new { Login = login, Password = ApiFixture.Password, Email = login + "@example.test", FullName = "Applicant", Organization = "Existing A", Inn = "7707083893", Phone = "123" })).EnsureSuccessStatusCode();
        Guid uid; using (var scope = f.Services.CreateScope()) uid = await scope.ServiceProvider.GetRequiredService<CubeDb>().Users.Where(x => x.UserName == login).Select(x => x.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/admin/registration-applications/{uid}/approve", new { ExistingTenantId = (Guid?)null })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/v1/admin/registration-applications/{uid}/approve", new { ExistingTenantId = f.TenantA })).EnsureSuccessStatusCode();
        using var user = await f.As(login); var me = await user.GetFromJsonAsync<JsonElement>("/api/v1/auth/me"); Assert.Equal(f.TenantA, me.GetProperty("tenantId").GetGuid());
    }
    [Fact]
    public async Task Request_status_uses_version_and_records_history()
    {
        using var a = await f.As("tenant-a"); using var staff = await f.As("staff");
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); var category = await db.Categories.FindAsync(f.Category); category!.ResponsibleUserId = f.Staff; await db.SaveChangesAsync(); }
        var created = await a.PostAsJsonAsync("/api/v1/requests", new { CategoryId = f.Category, PremisesId = f.PremisesA, Subject = "New issue", Description = "Please help", ContactName = "Contact", Phone = "1", Email = "a@example.test" }); created.EnsureSuccessStatusCode();
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = json.GetProperty("id").GetGuid(); var version = json.GetProperty("version").GetUInt32();
        var updated = await staff.PutAsJsonAsync($"/api/v1/requests/{id}/status", new { Status = "Accepted", Reason = "Assigned", Version = version }); updated.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PutAsJsonAsync($"/api/v1/requests/{id}/status", new { Status = "InProgress", Reason = "Stale", Version = version })).StatusCode);
        var history = await a.GetStringAsync($"/api/v1/requests/{id}"); Assert.Contains("Accepted", history);
    }
    [Fact]
    public async Task Optimistic_concurrency_protects_meter_watermark()
    {
        using var first = f.Services.CreateScope(); using var second = f.Services.CreateScope(); var db1 = first.ServiceProvider.GetRequiredService<CubeDb>(); var db2 = second.ServiceProvider.GetRequiredService<CubeDb>();
        var p = new Premises { Name = "Concurrency", Area = 10 }; db1.Add(p); var meter = new Meter { TenantId = f.TenantA, PremisesId = p.Id, SerialNumber = "concurrent-" + Guid.NewGuid(), LastValue = 10 }; db1.Add(meter); await db1.SaveChangesAsync();
        var stale = (await db2.Meters.FindAsync(meter.Id))!; meter.LastValue = 20; await db1.SaveChangesAsync(); stale.LastValue = 15; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }
    [Fact]
    public async Task Listing_inquiry_is_repeatable_and_does_not_reserve_premises()
    {
        Guid id; Guid pid; using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); var p = new Premises { Name = "Vacant", Area = 25, MonthlyRent = 100 }; var l = new Listing { PremisesId = p.Id, Title = "Vacant listing", Published = true }; db.AddRange(p, l); await db.SaveChangesAsync(); id = l.Id; pid = p.Id; }
        using var a = await f.As("tenant-a"); var input = new { ContactName = "A", Phone = "1", Email = "a@example.test", Message = "Interested" };
        var r1 = await a.PostAsJsonAsync($"/api/v1/listings/{id}/inquiries", input); r1.EnsureSuccessStatusCode(); var r2 = await a.PostAsJsonAsync($"/api/v1/listings/{id}/inquiries", input); r2.EnsureSuccessStatusCode(); Assert.Equal((await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid(), (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid());
        using var check = f.Services.CreateScope(); Assert.Equal("Available", (await check.ServiceProvider.GetRequiredService<CubeDb>().Premises.FindAsync(pid))!.Status);
    }
    [Fact]
    public async Task Scheduled_news_notifications_are_targeted_and_deduplicated()
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); await NotificationWorker.Tick(db, TimeProvider.System, default); await NotificationWorker.Tick(db, TimeProvider.System, default);
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == f.UserA && x.ResourceId == f.NewsA && x.Kind == "NewsPublished")); Assert.False(await db.Notifications.AnyAsync(x => x.UserId == f.UserB && x.ResourceId == f.NewsA));
    }

    [Fact]
    public async Task Repeated_staff_messages_create_separate_notifications()
    {
        using var staff = await f.As("staff");
        (await staff.PostAsJsonAsync($"/api/v1/requests/{f.RequestA}/messages", new { Text = "First reply", Internal = false })).EnsureSuccessStatusCode();
        (await staff.PostAsJsonAsync($"/api/v1/requests/{f.RequestA}/messages", new { Text = "Second reply", Internal = false })).EnsureSuccessStatusCode();
        using var scope = f.Services.CreateScope(); Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CubeDb>().Notifications.CountAsync(x => x.UserId == f.UserA && x.ResourceId == f.RequestA && x.Kind == "RequestMessage"));
    }

    [Fact]
    public async Task News_publication_and_expiry_boundaries_use_server_time()
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); var time = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var news = new News { Title = "scheduled boundary", Summary = "test", Body = "test", TenantIds = [f.TenantA], PublishAt = time.AddHours(1), ExpiresAt = time.AddHours(2) }; db.Add(news); await db.SaveChangesAsync();
        Assert.False(await NewsEndpoints.VisibleForTenant(f.TenantA, db, new FixedClock(time)).AnyAsync(x => x.Id == news.Id));
        Assert.True(await NewsEndpoints.VisibleForTenant(f.TenantA, db, new FixedClock(time.AddMinutes(90))).AnyAsync(x => x.Id == news.Id));
        Assert.False(await NewsEndpoints.VisibleForTenant(f.TenantA, db, new FixedClock(time.AddHours(2))).AnyAsync(x => x.Id == news.Id));
    }
    [Fact]
    public async Task Quarantined_uploads_are_not_downloadable()
    {
        using var isolated = f.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Files:AllowUnscannedDevelopmentUploads"] = "false" })));
        using var a = await f.As("tenant-a"); using var c = isolated.CreateClient(); c.DefaultRequestHeaders.Authorization = a.DefaultRequestHeaders.Authorization;
        using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent("%PDF-1.4 test"u8.ToArray()), "file", "quarantine.pdf"); var response = await c.PostAsync("/api/v1/files", form); response.EnsureSuccessStatusCode(); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Quarantined", json.GetProperty("state").GetString()); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/files/" + json.GetProperty("id").GetGuid())).StatusCode);
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    [Fact]
    public async Task Detail_routes_obey_tenant_boundaries()
    {
        using var a = await f.As("tenant-a"); using var b = await f.As("tenant-b");
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/v1/me/premises/{f.PremisesA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/me/premises/{f.PremisesA}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/v1/meters/{f.MeterA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/meters/{f.MeterA}")).StatusCode);
    }

    [Fact]
    public async Task Administration_catalogs_require_admin()
    {
        using var admin = await f.As("admin");
        var meters = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/meters?size=100");
        Assert.Contains(meters.EnumerateArray(), m => m.GetProperty("id").GetGuid() == f.MeterA);
        var leases = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/leases?tenantId={f.TenantA}");
        Assert.All(leases.EnumerateArray(), l => Assert.Equal(f.TenantA, l.GetProperty("tenantId").GetGuid()));
        using var tenant = await f.As("tenant-a"); using var staff = await f.As("staff");
        foreach (var path in new[] { "/api/v1/admin/meters", "/api/v1/admin/leases" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync("/api/v1/staff/meters")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync("/api/v1/staff/readings")).StatusCode);
    }
    [Fact]
    public async Task Employee_meter_history_follows_category_responsibility()
    {
        var login = "meter-worker-" + Guid.NewGuid().ToString("N"); Guid workerId; Guid categoryId; Guid? previous;
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var u = new User { Id = Guid.NewGuid(), UserName = login, Email = login + "@example.test", FullName = "Meter worker", Role = "Staff", Approved = true };
            Assert.True((await users.CreateAsync(u, ApiFixture.Password)).Succeeded); workerId = u.Id;
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); var category = await db.Categories.SingleAsync(c => c.Code == "electricity"); categoryId = category.Id; previous = category.ResponsibleUserId;
        }
        using var worker = await f.As(login);
        Assert.Empty((await worker.GetFromJsonAsync<JsonElement>("/api/v1/staff/meters")).EnumerateArray());
        try
        {
            using (var scope = f.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); (await db.Categories.FindAsync(categoryId))!.ResponsibleUserId = workerId;
                db.Meters.Add(new Meter { TenantId = f.TenantA, PremisesId = f.PremisesA, Type = "Water", SerialNumber = login, Unit = "m3" }); await db.SaveChangesAsync();
            }
            var meters = await worker.GetFromJsonAsync<JsonElement>("/api/v1/staff/meters?size=100");
            Assert.NotEmpty(meters.EnumerateArray()); Assert.All(meters.EnumerateArray(), m => Assert.Equal("Electricity", m.GetProperty("type").GetString()));
            var readings = await worker.GetFromJsonAsync<JsonElement>("/api/v1/staff/readings?size=100"); var ids = meters.EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToHashSet();
            Assert.All(readings.EnumerateArray(), r => Assert.Contains(r.GetProperty("meterId").GetGuid(), ids));
        }
        finally { using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); (await db.Categories.FindAsync(categoryId))!.ResponsibleUserId = previous; await db.SaveChangesAsync(); }
        Assert.Empty((await worker.GetFromJsonAsync<JsonElement>("/api/v1/staff/meters")).EnumerateArray());
    }

    [Fact]
    public async Task Staff_request_list_exposes_names_only_for_assigned_requests()
    {
        using var staff = await f.As("staff");
        var list = await staff.GetFromJsonAsync<JsonElement>("/api/v1/requests?size=100");
        var rows = list.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(rows, r => r.GetProperty("id").GetGuid() == f.RequestA && r.GetProperty("tenantName").GetString() == "A");
        Assert.DoesNotContain(rows, r => r.GetProperty("id").GetGuid() == f.RequestB);
        Assert.All(rows, r => Assert.Equal(f.Staff, r.GetProperty("assigneeId").GetGuid()));
    }
}

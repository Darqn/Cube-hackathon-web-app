using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cube.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cube.Tests;

public sealed class DemoSeedTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Demo_seed_is_atomic_repeatable_and_visible_through_authenticated_endpoints()
    {
        async Task SeedWith(string password)
        {
            using var scope = fixture.Services.CreateScope();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Password"] = password }).Build();
            await DemoSeed.Run(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IHostEnvironment>(), config);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedWith("weak"));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            Assert.False(await db.Tenants.AnyAsync(x => x.Name.StartsWith("ДЕМО")));
            Assert.False(await db.Audit.AnyAsync(x => x.Action == DemoSeed.Marker));
        }
        await SeedWith(ApiFixture.Password);
        await SeedWith("must-not-reset-password");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            Assert.Equal(5, await db.Users.CountAsync(x => x.UserName!.StartsWith("demo-")));
            Assert.Equal(2, await db.Tenants.CountAsync(x => x.Name.StartsWith("ДЕМО")));
            Assert.Equal(8, await db.Invoices.CountAsync(x => x.ExternalId.StartsWith("demo:")));
            Assert.Equal(14, await db.Requests.CountAsync(x => x.Subject.StartsWith("ДЕМО")));
            Assert.Equal(1, await db.Audit.CountAsync(x => x.Action == DemoSeed.Marker));
            foreach (var invoice in await db.Invoices.Where(x => x.ExternalId.StartsWith("demo:")).ToListAsync())
                Assert.Equal(invoice.Paid, await db.Set<PaymentRecord>().Where(x => x.InvoiceId == invoice.Id).SumAsync(x => x.Amount));
            Assert.True(await db.Users.AnyAsync(x => x.UserName == "admin"));
        }
        using var tenant = await fixture.As("demo-tenant");
        using var other = await fixture.As("demo-tenant-2");
        using var admin = await fixture.As("demo-admin");
        using var staff = await fixture.As("demo-staff");
        foreach (var endpoint in new[] { "dashboard", "invoices", "payments", "meters", "documents", "listings", "news", "notifications", "me/leases" })
            (await tenant.GetAsync("/api/v1/" + endpoint)).EnsureSuccessStatusCode();
        (await admin.GetAsync("/api/v1/admin/registration-applications")).EnsureSuccessStatusCode();
        var staffDashboard = await staff.GetFromJsonAsync<JsonElement>("/api/v1/staff/dashboard");
        Assert.Equal(14, staffDashboard.GetProperty("assigned").GetInt32());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            var user = await db.Users.SingleAsync(x => x.UserName == "demo-tenant");
            var document = await db.Documents.SingleAsync(x => x.TenantId == user.TenantId);
            var response = await tenant.GetAsync($"/api/v1/files/{document.FileId}");
            response.EnsureSuccessStatusCode();
            Assert.StartsWith("%PDF-1.4", await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/files/{document.FileId}")).StatusCode);
        }
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new
        {
            LoginName = "demo-pending",
            ApiFixture.Password
        })).StatusCode);
    }
}

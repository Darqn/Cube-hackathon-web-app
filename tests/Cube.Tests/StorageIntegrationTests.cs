using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cube.Api;
using Cube.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cube.Tests;

public sealed class StorageIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Real_database_failure_removes_the_physical_upload()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        var storage = scope.ServiceProvider.GetRequiredService<PrivateStorage>();
        var batch = scope.ServiceProvider.GetRequiredService<FileWriteBatch>();
        var file = new StoredFile { TenantId = Guid.NewGuid(), Key = Guid.NewGuid().ToString("N"), Name = "failed.pdf", State = "Ready" };
        db.Files.Add(file);
        await Assert.ThrowsAsync<DbUpdateException>(() => batch.SaveAsync([new(file.Key, "%PDF-test"u8.ToArray())], async token => { await db.SaveChangesAsync(token); }, default));
        Assert.False(File.Exists(storage.PathFor(file.Key)));
        db.ChangeTracker.Clear();
        Assert.False(await db.Files.AnyAsync(x => x.Id == file.Id));
    }

    [Fact]
    public async Task News_attachment_visibility_follows_the_news_audience()
    {
        using var admin = await fixture.As("admin");
        using var form = Upload("notice.pdf", "%PDF-1.4\nnotice"u8.ToArray());
        var response = await admin.PostAsync($"/api/v1/admin/news/{fixture.NewsA}/attachments", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal("Ready", body.GetProperty("state").GetString());
        using var tenant = await fixture.As("tenant-a");
        using var other = await fixture.As("tenant-b");
        var path = $"/api/v1/news/{fixture.NewsA}/attachments/{id}";
        Assert.Equal("%PDF-1.4\nnotice", await tenant.GetStringAsync(path));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Listing_upload_rejects_documents_and_archiving_revokes_photo_access()
    {
        Guid id;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            var premises = new Premises { Name = "Photo test", Area = 20 };
            var listing = new Listing { PremisesId = premises.Id, Published = true, Title = "Photo test" };
            db.AddRange(premises, listing);
            await db.SaveChangesAsync();
            id = listing.Id;
        }
        using var admin = await fixture.As("admin");
        using (var pdf = Upload("not-a-photo.pdf", "%PDF-1.4\ntest"u8.ToArray()))
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/v1/admin/listings/{id}/photos", pdf)).StatusCode);
        using var png = Upload("photo.png", [137, 80, 78, 71, 13, 10, 26, 10, 0]);
        var uploaded = await admin.PostAsync($"/api/v1/admin/listings/{id}/photos", png);
        uploaded.EnsureSuccessStatusCode();
        var asset = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var tenant = await fixture.As("tenant-a");
        var path = $"/api/v1/listings/{id}/photos/{asset}";
        Assert.Equal(HttpStatusCode.OK, (await tenant.GetAsync(path)).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            (await db.Listings.FindAsync(id))!.Archived = true;
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Set<PublishedAsset>().CountAsync(x => x.ListingId == id));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
    }

    private static MultipartFormDataContent Upload(string name, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return form;
    }
}

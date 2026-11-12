using Cube.Api.Api.Files;
using Cube.Api.Infrastructure.Storage;
using Cube.Api.Modules.News;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class MediaEndpoints
{
    public static void MapMedia(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Published media");
        g.MapPost("/admin/listings/{id:guid}/photos", async (Guid id, HttpRequest request, CubeDb db, PrivateStorage storage, FileWriteBatch files, CancellationToken ct) => { Guard.That(await db.Listings.AnyAsync(x => x.Id == id, ct), "listing_not_found", 404); return await Upload(id, null, request, db, storage, files, ct); }).RequireAuthorization("Admin");
        g.MapPost("/admin/news/{id:guid}/attachments", async (Guid id, HttpRequest request, CubeDb db, PrivateStorage storage, FileWriteBatch files, CancellationToken ct) => { Guard.That(await db.News.AnyAsync(x => x.Id == id, ct), "news_not_found", 404); return await Upload(null, id, request, db, storage, files, ct); }).RequireAuthorization("Admin");
        g.MapGet("/listings/{id:guid}/photos", async (Guid id, CubeDb db, Access a, CancellationToken ct) => { await ListingVisible(id, a, db, ct); return Results.Ok(await db.Set<PublishedAsset>().Where(x => x.ListingId == id && x.State == "Ready").OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.Name, Url = "/api/v1/listings/" + id + "/photos/" + x.Id }).ToListAsync(ct)); });
        g.MapGet("/listings/{id:guid}/photos/{assetId:guid}", async (Guid id, Guid assetId, CubeDb db, Access a, PrivateStorage storage, CancellationToken ct) => { await ListingVisible(id, a, db, ct); var asset = await db.Set<PublishedAsset>().SingleOrDefaultAsync(x => x.Id == assetId && x.ListingId == id && x.State == "Ready", ct) ?? throw new ApiError(404, "asset_not_found"); return Download(asset, storage); });
        g.MapGet("/news/{id:guid}/attachments", async (Guid id, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => { await NewsVisible(id, a, db, clock, ct); return Results.Ok(await db.Set<PublishedAsset>().Where(x => x.NewsId == id && x.State == "Ready").Select(x => new { x.Id, x.Name, Url = "/api/v1/news/" + id + "/attachments/" + x.Id }).ToListAsync(ct)); });
        g.MapGet("/news/{id:guid}/attachments/{assetId:guid}", async (Guid id, Guid assetId, Access a, CubeDb db, TimeProvider clock, PrivateStorage storage, CancellationToken ct) => { await NewsVisible(id, a, db, clock, ct); var asset = await db.Set<PublishedAsset>().SingleOrDefaultAsync(x => x.Id == assetId && x.NewsId == id && x.State == "Ready", ct) ?? throw new ApiError(404, "asset_not_found"); return Download(asset, storage); });
    }
    private static async Task ListingVisible(Guid id, Access a, CubeDb db, CancellationToken ct) => Guard.That(await (from l in db.Listings join p in db.Premises on l.PremisesId equals p.Id where l.Id == id && (a.Admin || (l.Published && !l.Archived && (p.Status == "Available" || p.Status == "Reserved"))) select l.Id).AnyAsync(ct), "listing_not_found", 404);
    private static async Task NewsVisible(Guid id, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => Guard.That(a.Admin ? await db.News.AnyAsync(x => x.Id == id, ct) : await NewsAudience.ForTenant(a.TenantId, db, clock).AnyAsync(x => x.Id == id, ct), "news_not_found", 404);
    private static IResult Download(PublishedAsset asset, PrivateStorage storage) { var path = storage.PathFor(asset.Key); Guard.That(File.Exists(path), "asset_not_found", 404); return Results.File(path, asset.ContentType, asset.Name, enableRangeProcessing: true); }
    private static async Task<IResult> Upload(Guid? listingId, Guid? newsId, HttpRequest request, CubeDb db, PrivateStorage storage, FileWriteBatch files, CancellationToken ct)
    {
        Guard.That(request.HasFormContentType, "multipart_required");
        var form = await request.ReadFormAsync(ct);
        Guard.That(form.Files.Count == 1, "one_file_required");
        var upload = await UploadedFileReader.ReadAsync(form.Files[0], ct);
        Guard.That(listingId is null || upload.ContentType.StartsWith("image/", StringComparison.Ordinal), "photo_required");
        var asset = new PublishedAsset { ListingId = listingId, NewsId = newsId, Key = Guid.NewGuid().ToString("N"), Name = upload.Name, ContentType = upload.ContentType, State = storage.AllowDevelopmentFiles ? "Ready" : "Quarantined" };
        db.Add(asset);
        await files.SaveAsync([new(asset.Key, upload.Bytes)], async token => { await db.SaveChangesAsync(token); }, ct);
        return Results.Created(listingId is Guid l ? $"/api/v1/listings/{l}/photos/{asset.Id}" : $"/api/v1/news/{newsId}/attachments/{asset.Id}", new { asset.Id, asset.Name, asset.State });
    }
}

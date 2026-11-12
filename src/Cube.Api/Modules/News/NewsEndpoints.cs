using Cube.Api.Modules.News;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class NewsEndpoints
{
    public record Input(string Title, string Summary, string Body, string Category, string Importance, DateTimeOffset? PublishAt, DateTimeOffset? ExpiresAt, bool AllTenants, Guid[] TenantIds, Guid[] PremisesIds, string? Building, string? Section, int? Floor, bool RequiresAcknowledgement) : IApiRequest;
    public static void MapNews(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("News");
        g.MapGet("/news", async (Access a, CubeDb db, TimeProvider clock, string? category, bool? unreadOnly, int? page, int? size, CancellationToken ct) =>
        {
            var q = NewsAudience.ForTenant(a.TenantId, db, clock).Where(x => category == null || x.Category == category);
            if (unreadOnly == true)
                q = q.Where(n => !db.Receipts.Any(r => r.NewsId == n.Id && r.UserId == a.UserId && r.ReadAt != null));
            var items = await q.OrderByDescending(x => x.Importance == "Urgent").ThenByDescending(x => x.Importance == "Important").ThenByDescending(x => x.PublishAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct);
            var ids = items.Select(x => x.Id).ToArray();
            var receipts = await db.Receipts.Where(x => x.UserId == a.UserId && x.TenantId == a.TenantId && ids.Contains(x.NewsId)).ToListAsync(ct);
            return Results.Ok(new
            {
                Total = await q.CountAsync(ct),
                Items = items.Select(n => new { News = NewsResponses.Dto(n), ReadAt = receipts.SingleOrDefault(r => r.NewsId == n.Id)?.ReadAt, AcknowledgedAt = receipts.SingleOrDefault(r => r.NewsId == n.Id)?.AcknowledgedAt })
            });
        });
        g.MapGet("/news/{id:guid}", async (Guid id, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => { var n = await NewsAudience.ForTenant(a.TenantId, db, clock).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "news_not_found"); return Results.Ok(NewsResponses.Dto(n)); });
        g.MapPost("/news/{id:guid}/read", async (Guid id, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => { await Receipt(id, false, a, db, clock, ct); return Results.NoContent(); });
        g.MapPost("/news/{id:guid}/acknowledge", async (Guid id, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => { await Receipt(id, true, a, db, clock, ct); return Results.NoContent(); });
        g.MapGet("/admin/news", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.News.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPost("/admin/news", async (Input i, CubeDb db, CancellationToken ct) => { var n = new News(); await Set(n, i, db, ct); db.News.Add(n); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/news/{n.Id}", n); }).RequireAuthorization("Admin");
        g.MapPut("/admin/news/{id:guid}", async (Guid id, Input i, CubeDb db, CancellationToken ct) => { var n = await db.News.FindAsync([id], ct) ?? throw new ApiError(404, "news_not_found"); await Set(n, i, db, ct); await db.SaveChangesAsync(ct); return Results.Ok(n); }).RequireAuthorization("Admin");
        g.MapDelete("/admin/news/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => { var n = await db.News.FindAsync([id], ct) ?? throw new ApiError(404, "news_not_found"); n.Archived = true; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization("Admin");
        g.MapGet("/admin/news/{id:guid}/receipts", async (Guid id, CubeDb db, CancellationToken ct) => await db.Receipts.Where(x => x.NewsId == id).Select(x => new { x.TenantId, x.UserId, x.ReadAt, x.AcknowledgedAt }).Take(1000).ToListAsync(ct)).RequireAuthorization("Admin");
    }
    private static async Task Receipt(Guid id, bool acknowledge, Access a, CubeDb db, TimeProvider clock, CancellationToken ct)
    {
        var n = await NewsAudience.ForTenant(a.TenantId, db, clock).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "news_not_found");
        Guard.That(!acknowledge || n.RequiresAcknowledgement, "acknowledgement_not_required");
        // Atomic upsert makes retries/concurrent read+ack safe, without conflating their meanings.
        var now = clock.GetUtcNow();
        DateTimeOffset? ack = acknowledge ? now : null;
        var user = a.UserId;
        var tenant = a.TenantId;
        var rid = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Receipts\" (\"Id\",\"CreatedAt\",\"TenantId\",\"NewsId\",\"UserId\",\"ReadAt\",\"AcknowledgedAt\") VALUES ({rid},{now},{tenant},{id},{user},{now},{ack}) ON CONFLICT (\"NewsId\",\"UserId\") DO UPDATE SET \"ReadAt\" = COALESCE(\"Receipts\".\"ReadAt\",EXCLUDED.\"ReadAt\"), \"AcknowledgedAt\" = COALESCE(\"Receipts\".\"AcknowledgedAt\",EXCLUDED.\"AcknowledgedAt\")", ct);
    }
    private static async Task Set(News n, Input i, CubeDb db, CancellationToken ct)
    {
        Guard.That(i.Importance is "Normal" or "Important" or "Urgent", "invalid_importance");
        Guard.That(new[] { "Emergency", "PlannedWork", "Electricity", "Water", "Heating", "Construction", "Accounting", "Rent", "General" }.Contains(i.Category), "invalid_category");
        Guard.That(i.ExpiresAt == null || (i.PublishAt != null && i.ExpiresAt > i.PublishAt), "invalid_expiry");
        Guard.That(i.AllTenants || i.TenantIds.Length > 0 || i.PremisesIds.Length > 0 || i.Building != null || i.Section != null || i.Floor != null, "audience_required");
        Guard.That(i.TenantIds.Length <= 1000 && i.PremisesIds.Length <= 1000, "audience_too_large");
        Guard.That(await db.Tenants.CountAsync(x => i.TenantIds.Contains(x.Id), ct) == i.TenantIds.Distinct().Count(), "invalid_tenants");
        Guard.That(await db.Premises.CountAsync(x => i.PremisesIds.Contains(x.Id), ct) == i.PremisesIds.Distinct().Count(), "invalid_premises");
        n.Title = Guard.Text(i.Title, "title");
        n.Summary = Guard.Text(i.Summary, "summary", 1000);
        n.Body = Guard.Text(i.Body, "body", 10000);
        n.Category = i.Category;
        n.Importance = i.Importance;
        n.PublishAt = i.PublishAt;
        n.ExpiresAt = i.ExpiresAt;
        n.AllTenants = i.AllTenants;
        n.TenantIds = i.TenantIds.Distinct().ToArray();
        n.PremisesIds = i.PremisesIds.Distinct().ToArray();
        n.Building = i.Building;
        n.Section = i.Section;
        n.Floor = i.Floor;
        n.RequiresAcknowledgement = i.RequiresAcknowledgement;
    }
}

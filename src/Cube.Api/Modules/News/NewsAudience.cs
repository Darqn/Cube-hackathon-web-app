using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.News;

public static class NewsAudience
{
    public static IQueryable<global::Cube.Api.News> ForTenant(Guid tenant, CubeDb db, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var occupied = from l in db.Leases join p in db.Premises on l.PremisesId equals p.Id where l.TenantId == tenant && l.Status == "Active" && l.StartsOn <= today && l.EndsOn >= today select p;
        // Live targeting: access follows current active leases, not a stale client-supplied location.
        return db.News.Where(n => !n.Archived && n.PublishAt != null && n.PublishAt <= now && (n.ExpiresAt == null || n.ExpiresAt > now) && (n.AllTenants || n.TenantIds.Contains(tenant) || occupied.Any(p => n.PremisesIds.Contains(p.Id) || ((n.Building != null || n.Section != null || n.Floor != null) && (n.Building == null || n.Building == p.Building) && (n.Section == null || n.Section == p.Section) && (n.Floor == null || n.Floor == p.Floor)))));
    }
}

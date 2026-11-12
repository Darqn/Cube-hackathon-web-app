using Cube.Api.Modules.News;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Notifications.Features;

public sealed class NotificationScheduler(CubeDb db, TimeProvider clock)
{
    public async Task Tick(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var users = await db.Users.AsNoTracking().Where(u => u.TenantId != null && u.Approved && !u.Blocked && db.Tenants.Any(t => t.Id == u.TenantId && !t.Blocked)).ToListAsync(ct);
        foreach (var user in users)
        {
            var tenant = user.TenantId!.Value;
            if (user.NewsNotifications)
            foreach (var n in await NewsAudience.ForTenant(tenant, db, clock).Select(x => new { x.Id, x.Title }).ToListAsync(ct))
                await Add(db, tenant, user.Id, "NewsPublished", n.Id, n.Title, now, ct);
            if (user.InvoiceReminders)
            foreach (var i in await db.Invoices.AsNoTracking().Where(x => x.TenantId == tenant && x.Paid < x.Amount && x.DueOn <= today.AddDays(3)).Select(x => new { x.Id, x.DueOn }).ToListAsync(ct))
                await Add(db, tenant, user.Id, i.DueOn < today ? "InvoiceOverdue" : "InvoiceDueSoon", i.Id, "Please review your outstanding invoice", now, ct);
            if (user.MeterReminders && today.Day >= 25)
            {
                var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                foreach (var id in await db.Meters.AsNoTracking().Where(x => x.TenantId == tenant && x.Active && !db.Readings.Any(r => r.MeterId == x.Id && r.CreatedAt >= monthStart) && db.Leases.Any(l => l.TenantId == tenant && l.PremisesId == x.PremisesId && l.Status == "Active" && l.StartsOn <= today && l.EndsOn >= today)).Select(x => x.Id).ToListAsync(ct))
                    await Add(db, tenant, user.Id, $"MeterReminder:{today:yyyy-MM}", id, "Please submit your meter reading", now, ct);
            }
        }
    }
    private static async Task Add(CubeDb db, Guid tenant, Guid user, string kind, Guid resource, string text, DateTimeOffset now, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var deduplicationKey = kind + ":" + resource;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Notifications\" (\"Id\",\"CreatedAt\",\"TenantId\",\"UserId\",\"Kind\",\"ResourceId\",\"Text\",\"Read\",\"DeduplicationKey\") VALUES ({id},{now},{tenant},{user},{kind},{resource},{text},false,{deduplicationKey}) ON CONFLICT (\"UserId\",\"DeduplicationKey\") DO NOTHING", ct);
    }
}

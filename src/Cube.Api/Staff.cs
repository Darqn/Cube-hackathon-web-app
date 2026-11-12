using Cube.Api.Modules.Metering;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class StaffEndpoints
{
    public static IQueryable<Notification> Notifications(Access a, CubeDb db, MeteringVisibility visibility)
    {
        var requests = a.Requests; var readings = visibility.Readings();
        return db.Notifications.Where(n => n.UserId == a.UserId && n.ResourceId != null && ((n.Kind.StartsWith("StaffRequest") && requests.Any(r => r.Id == n.ResourceId && r.TenantId == n.TenantId)) || (n.Kind == "StaffReadingSubmitted" && readings.Any(r => r.Id == n.ResourceId && r.TenantId == n.TenantId))));
    }
    public static void MapStaff(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/staff").AddEndpointFilter<InputValidationFilter>().RequireAuthorization(p => p.RequireRole("Staff", "Admin")).WithTags("Staff workspace");
        g.MapGet("/dashboard", async (Access a, CubeDb db, MeteringVisibility visibility, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow(); var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); var tomorrow = day.AddDays(1);
            var active = a.Requests.Where(r => r.Status != "Closed" && r.Status != "Rejected" && r.Status != "Completed");
            return Results.Ok(new
            {
                Assigned = await a.Requests.CountAsync(ct),
                Active = await active.CountAsync(ct),
                New = await active.CountAsync(r => r.Status == "New", ct),
                WaitingForTenant = await active.CountAsync(r => r.Status == "Clarification", ct),
                Overdue = await active.CountAsync(r => r.DueAt != null && r.DueAt < now, ct),
                DueToday = await active.CountAsync(r => r.DueAt >= day && r.DueAt < tomorrow, ct),
                UnreadNotifications = await Notifications(a, db, visibility).CountAsync(n => !n.Read, ct),
                PendingReadings = await visibility.Readings().CountAsync(r => r.Status == "Submitted", ct),
                Directions = await db.Categories.Where(c => a.Admin || c.ResponsibleUserId == a.UserId).OrderBy(c => c.Code).Select(c => new { c.Id, c.Code, c.Name, c.Department }).ToListAsync(ct)
            });
        });
        g.MapGet("/notifications", async (Access a, CubeDb db, MeteringVisibility visibility, bool? unreadOnly, int? page, int? size, CancellationToken ct) =>
            await Notifications(a, db, visibility).AsNoTracking().Where(n => unreadOnly != true || !n.Read).OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/notifications/{id:guid}/read", async (Guid id, Access a, CubeDb db, MeteringVisibility visibility, CancellationToken ct) =>
        {
            var n = await Notifications(a, db, visibility).SingleOrDefaultAsync(n => n.Id == id, ct) ?? throw new ApiError(404, "notification_not_found"); n.Read = true; await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        g.MapPost("/notifications/read-all", async (Access a, CubeDb db, MeteringVisibility visibility, CancellationToken ct) =>
        {
            var changed = await Notifications(a, db, visibility).Where(n => !n.Read).ExecuteUpdateAsync(p => p.SetProperty(n => n.Read, true), ct);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = "ReadStaffNotifications", ResourceId = a.UserId }); await db.SaveChangesAsync(ct); return Results.Ok(new { Updated = changed });
        });

    }
}

using Cube.Api.Modules.Notifications;
using Cube.Api.Modules.Metering;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class StaffDashboardEndpoints
{
    public static void MapStaffDashboard(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/staff").AddEndpointFilter<InputValidationFilter>().RequireAuthorization(p => p.RequireRole("Staff", "Admin")).WithTags("Staff workspace");
        g.MapGet("/dashboard", async (Access a, CubeDb db, MeteringVisibility visibility, StaffNotificationVisibility notices, TimeProvider clock, CancellationToken ct) =>
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
                UnreadNotifications = await notices.Query().CountAsync(n => !n.Read, ct),
                PendingReadings = await visibility.Readings().CountAsync(r => r.Status == "Submitted", ct),
                Directions = await db.Categories.Where(c => a.Admin || c.ResponsibleUserId == a.UserId).OrderBy(c => c.Code).Select(c => new { c.Id, c.Code, c.Name, c.Department }).ToListAsync(ct)
            });
        });
    }
}

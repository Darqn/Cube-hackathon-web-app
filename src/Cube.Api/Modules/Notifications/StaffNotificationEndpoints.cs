using Cube.Api.Modules.Metering;
using Cube.Api.Modules.Notifications;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class StaffNotificationEndpoints
{
    public static void MapStaffNotifications(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/staff").AddEndpointFilter<InputValidationFilter>().RequireAuthorization(p => p.RequireRole("Staff", "Admin")).WithTags("Staff workspace");
        g.MapGet("/notifications", async (Access a, CubeDb db, MeteringVisibility visibility, StaffNotificationVisibility notices, bool? unreadOnly, int? page, int? size, CancellationToken ct) =>
            await notices.Query().AsNoTracking().Where(n => unreadOnly != true || !n.Read).OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/notifications/{id:guid}/read", async (Guid id, Access a, CubeDb db, MeteringVisibility visibility, StaffNotificationVisibility notices, CancellationToken ct) =>
        {
            var n = await notices.Query().SingleOrDefaultAsync(n => n.Id == id, ct) ?? throw new ApiError(404, "notification_not_found");
            n.Read = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        g.MapPost("/notifications/read-all", async (Access a, CubeDb db, MeteringVisibility visibility, StaffNotificationVisibility notices, CancellationToken ct) =>
        {
            var changed = await notices.Query().Where(n => !n.Read).ExecuteUpdateAsync(p => p.SetProperty(n => n.Read, true), ct);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = "ReadStaffNotifications", ResourceId = a.UserId });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                Updated = changed
            });
        });

    }
}

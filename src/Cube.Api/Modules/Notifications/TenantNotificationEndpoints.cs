using Microsoft.EntityFrameworkCore;
using static Cube.Api.TenantEndpoints;
using Cube.Api.Workflows;
using Cube.Api.Modules.Billing.Domain;

namespace Cube.Api;

public static class NotificationsTenantEndpoints
{
    public static void MapTenantNotifications(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Tenant portal");
        g.MapGet("/notifications", async (Access a, bool? unreadOnly, int? page, int? size, CancellationToken ct) => await a.Own<Notification>().Where(x => (x.UserId == null || x.UserId == a.UserId) && (unreadOnly != true || !x.Read)).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/notifications/{id:guid}/read", async (Guid id, Access a, CubeDb db, CancellationToken ct) => { var n = await a.Own<Notification>().SingleOrDefaultAsync(x => x.Id == id && (x.UserId == null || x.UserId == a.UserId), ct) ?? throw new ApiError(404, "notification_not_found"); n.Read = true; await db.SaveChangesAsync(ct); return Results.NoContent(); });

    }
}

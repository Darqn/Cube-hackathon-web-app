using Cube.Api.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Cube.Api.AdminEndpoints;

namespace Cube.Api;

public static class RoutingAdminEndpoints
{
    public static void MapAdminRouting(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").AddEndpointFilter<InputValidationFilter>().RequireAuthorization("Admin").WithTags("Administration");
        g.MapPut("/request-categories/{id:guid}/routing", async (Guid id, Routing i, CubeDb db, CancellationToken ct) =>
        {
            var c = await db.Categories.FindAsync([id], ct) ?? throw new ApiError(404, "category_not_found");
            if (i.ResponsibleUserId is Guid uid)
                Guard.That(await db.Users.AnyAsync(x => x.Id == uid && (x.Role == "Staff" || x.Role == "Admin") && x.Approved && !x.Blocked, ct), "invalid_assignee");
            c.ResponsibleUserId = i.ResponsibleUserId;
            await db.SaveChangesAsync(ct);
            return Results.Ok(c);
        });

    }
}

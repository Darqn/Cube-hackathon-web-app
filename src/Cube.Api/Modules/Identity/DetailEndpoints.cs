using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class IdentityDetailEndpoints
{
    public static void MapIdentityDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/admin/users/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Users.Where(x => x.Id == id).Select(x => new { x.Id, x.UserName, x.Email, x.FullName, x.PhoneNumber, x.TenantId, x.Role, x.Approved, x.Blocked }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");

    }
}

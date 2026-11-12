using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class PropertyDetailEndpoints
{
    public static void MapPropertyDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/me/leases/{id:guid}", async (Guid id, Access a, CubeDb db, CancellationToken ct) => await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id where l.Id == id select new { Lease = l, Premises = p }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound());
        g.MapGet("/me/premises/{id:guid}", async (Guid id, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => { var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime); return await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id where p.Id == id && l.Status == "Active" && l.StartsOn <= today && l.EndsOn >= today select new { Lease = l, Premises = p }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound(); });
        g.MapGet("/admin/premises/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Premises.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");

    }
}

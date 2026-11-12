using Microsoft.EntityFrameworkCore;
using static Cube.Api.TenantEndpoints;
using Cube.Api.Workflows;
using Cube.Api.Modules.Billing.Domain;

namespace Cube.Api;

public static class PropertyTenantEndpoints
{
    public static void MapTenantProperty(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Tenant portal");
        g.MapGet("/me/leases", async (Access a, CubeDb db, int? page, int? size, CancellationToken ct) => await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id orderby l.StartsOn descending, l.Id select new { Lease = l, Premises = p }).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/me/premises", async (Access a, CubeDb db, TimeProvider clock, CancellationToken ct) => { var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime); return await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id where l.Status == "Active" && l.StartsOn <= today && l.EndsOn >= today select new { Lease = l, Premises = p }).Take(100).ToListAsync(ct); });

    }
}

using Microsoft.EntityFrameworkCore;
using static Cube.Api.AdminEndpoints;
using Microsoft.AspNetCore.Identity;
using Cube.Api.Modules.Identity.Domain;

namespace Cube.Api;

public static class MeteringAdminEndpoints
{
    public static void MapAdminMetering(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").AddEndpointFilter<InputValidationFilter>().RequireAuthorization("Admin").WithTags("Administration");
        g.MapPost("/meters", async (MeterInput i, CubeDb db, Access a, CancellationToken ct) =>
        {
            Guard.That(i.Type is "Water" or "Electricity" && i.Baseline >= 0, "invalid_meter"); await a.ActivePremises(i.TenantId, i.PremisesId, ct);
            var m = new Meter { TenantId = i.TenantId, PremisesId = i.PremisesId, Type = i.Type, SerialNumber = Guard.Text(i.SerialNumber, "serial"), Unit = Guard.Text(i.Unit, "unit", 30), LastValue = i.Baseline }; db.Meters.Add(m); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/meters/{m.Id}", m);
        });
        g.MapPut("/meters/{id:guid}/baseline", async (Guid id, MeterBaseline i, CubeDb db, CancellationToken ct) =>
        {
            var m = await db.Meters.FindAsync([id], ct) ?? throw new ApiError(404, "meter_not_found"); Guard.That(i.Value >= 0, "invalid_baseline"); Guard.That(i.Version == m.Version, "stale_version", 409); Guard.Text(i.Reason, "reason", 2000);
            // Corrections are separate history records; never rewrite a submitted reading.
            db.Readings.Add(new Reading { TenantId = m.TenantId, MeterId = id, PreviousValue = i.Value, Value = i.Value, Status = "Correction", ReviewReason = i.Reason }); m.LastValue = i.Value; await db.SaveChangesAsync(ct); return Results.Ok(m);
        });
        g.MapGet("/meters", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Meters.AsNoTracking().Where(x => tenantId == null || x.TenantId == tenantId).OrderBy(x => x.SerialNumber).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));

    }
}

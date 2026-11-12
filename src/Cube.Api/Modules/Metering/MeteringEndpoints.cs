using Cube.Api.Contracts;
using Cube.Api.Modules.Metering;
using Cube.Api.Modules.Metering.Features;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class MeteringEndpoints
{
    public record ReadingInput(decimal Value, Guid? PhotoId) : IApiRequest;
    public static void MapMetering(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Requests and readings");
        g.MapGet("/meters", async (Access a, CubeDb db, CancellationToken ct) => await a.Own<Meter>().AsNoTracking().OrderBy(x => x.SerialNumber).ToListAsync(ct));
        g.MapGet("/meters/{id:guid}/readings", async (Guid id, Access a, int? page, int? size, CancellationToken ct) =>
        {
            Guard.That(await a.Own<Meter>().AnyAsync(x => x.Id == id, ct), "meter_not_found", 404);
            return Results.Ok(await a.Own<Reading>().Where(x => x.MeterId == id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        });
        g.MapPost("/meters/{id:guid}/readings", async (Guid id, ReadingInput input, SubmitReadingHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.Handle(id, input.Value, input.PhotoId, ct)).ToHttpResult(context, r => Results.Created($"/api/v1/meters/{id}/readings", r)));
        g.MapGet("/staff/meters", async (Access a, MeteringVisibility visibility, CubeDb db, int? page, int? size, CancellationToken ct) =>
        {
            Guard.That(a.Staff, "staff_required", 403);
            return Results.Ok(await visibility.Meters().AsNoTracking().OrderBy(m => m.SerialNumber).ThenBy(m => m.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(m => new { m.Id, m.TenantId, m.PremisesId, m.Type, m.SerialNumber, m.Unit, m.LastValue, m.Active, m.Version, m.CreatedAt, TenantName = db.Tenants.Where(t => t.Id == m.TenantId).Select(t => t.Name).FirstOrDefault(), PremisesName = db.Premises.Where(p => p.Id == m.PremisesId).Select(p => p.Name).FirstOrDefault() }).ToListAsync(ct));
        });
        g.MapGet("/staff/readings", async (Access a, MeteringVisibility visibility, CubeDb db, int? page, int? size, CancellationToken ct) =>
        {
            Guard.That(a.Staff, "staff_required", 403);
            return Results.Ok(await visibility.Readings().AsNoTracking().OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        });
        g.MapGet("/admin/readings", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Readings.Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPut("/admin/readings/{id:guid}/review", ReviewReading).RequireAuthorization("Admin");
    }

    public static void MapStaffMetering(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/staff").AddEndpointFilter<InputValidationFilter>().RequireAuthorization(p => p.RequireRole("Staff", "Admin")).WithTags("Staff workspace");
        g.MapPut("/readings/{id:guid}/review", ReviewReading);
        g.MapGet("/readings/{id:guid}/photo", async (Guid id, Access a, CubeDb db, MeteringVisibility visibility, PrivateStorage storage, CancellationToken ct) =>
        {
            var r = await visibility.Readings().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new ApiError(404, "reading_not_found");
            Guard.That(r.PhotoId.HasValue, "photo_not_available", 404);
            var file = await db.Files.SingleOrDefaultAsync(f => f.Id == r.PhotoId && f.TenantId == r.TenantId && f.State == "Ready", ct) ?? throw new ApiError(404, "photo_not_available");
            var path = storage.PathFor(file.Key);
            Guard.That(File.Exists(path), "photo_not_available", 404);
            return Results.File(path, file.ContentType, file.Name, enableRangeProcessing: true);
        });
    }

    private static async Task<IResult> ReviewReading(Guid id, ReviewInput input, ReviewReadingHandler handler, HttpContext context, CancellationToken ct)
    {
        var result = await handler.Handle(id, input.Status, input.Reason, ct);
        return result.ToHttpResult(context, r => Results.Ok(new { r.Id, r.Status, r.ReviewReason }));
    }
}

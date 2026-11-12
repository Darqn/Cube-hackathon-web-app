using Cube.Api.Common.Operations;
using Cube.Api.Modules.Metering.Domain;
using Cube.Api.Infrastructure.Persistence;
using Cube.Api.Modules.Property;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Metering.Features;

public sealed class SubmitReadingHandler(TenantScope tenant, ActiveLeases leases, CubeDb db)
{
    public async Task<Outcome<Reading>> Handle(Guid id, decimal value, Guid? photoId, CancellationToken ct)
    {
        var meter = await tenant.Own<Meter>().SingleOrDefaultAsync(x => x.Id == id && x.Active, ct);
        if (meter is null) return Outcome<Reading>.Reject(Failure.NotFound("meter_not_found"));
        if (!await leases.Contains(tenant.TenantId, meter.PremisesId, ct)) return Outcome<Reading>.Reject(Failure.NotFound("active_lease_required"));
        if (!ReadingRules.CanSubmit(meter.LastValue, value)) return Outcome<Reading>.Reject(Failure.Invalid("invalid_reading"));
        if (photoId is Guid file && !await db.Files.AnyAsync(x => x.Id == file && x.TenantId == tenant.TenantId && x.State == "Ready" && (x.ContentType == "image/jpeg" || x.ContentType == "image/png"), ct))
            return Outcome<Reading>.Reject(Failure.Invalid("photo_not_available"));

        var reading = new Reading { TenantId = tenant.TenantId, MeterId = id, PreviousValue = meter.LastValue, Value = value, PhotoId = photoId };
        meter.LastValue = value;
        db.Readings.Add(reading);
        await Events.NotifyReadingStaffAsync(db, meter, reading, ct);
        Events.Queue(db, "Bitrix", "ReadingSubmitted", reading.Id);
        await db.SaveChangesAsync(ct);
        return Outcome<Reading>.Success(reading);
    }
}

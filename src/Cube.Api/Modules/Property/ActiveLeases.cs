using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Property;

public sealed class ActiveLeases(CubeDb db, TimeProvider clock)
{
    public Task<bool> Contains(Guid tenant, Guid premises, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return db.Leases.AnyAsync(x => x.TenantId == tenant && x.PremisesId == premises && x.Status == "Active" && x.StartsOn <= today && x.EndsOn >= today, ct);
    }
}

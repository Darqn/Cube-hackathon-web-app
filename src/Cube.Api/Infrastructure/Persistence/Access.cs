using Cube.Api.Common.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public sealed class Access(ICurrentActor actor, CubeDb db, TimeProvider clock)
{
    public Guid UserId => actor.UserId ?? throw new InvalidOperationException("Authenticated user required");
    public bool Admin => actor.IsAdmin;
    public bool Staff => actor.IsStaff;
    public Guid TenantId => actor.TenantId ?? throw new ApiError(403, "tenant_context_required");
    public IQueryable<T> Own<T>() where T : Owned => db.Set<T>().Where(x => x.TenantId == TenantId);
    public IQueryable<ServiceRequest> Requests => Admin ? db.Requests : Staff ? db.Requests.Where(x => x.AssigneeId == UserId) : Own<ServiceRequest>();
    public async Task<ServiceRequest> Request(Guid id, CancellationToken ct) => await Requests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "request_not_found");
    public async Task ActivePremises(Guid tenant, Guid premises, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guard.That(await db.Leases.AnyAsync(x => x.TenantId == tenant && x.PremisesId == premises && x.Status == "Active" && x.StartsOn <= today && x.EndsOn >= today, ct), "active_lease_required", 404);
    }
}

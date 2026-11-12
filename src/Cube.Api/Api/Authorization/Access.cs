using Cube.Api.Common.Identity;
using Cube.Api.Infrastructure.Persistence;
using Cube.Api.Modules.Property;
using Cube.Api.Modules.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

// Compatibility adapter for simple HTTP queries. Feature handlers use the focused scopes directly.
public sealed class Access(ICurrentActor actor, TenantScope tenant, RequestVisibility requests, ActiveLeases leases)
{
    public Guid UserId => actor.UserId ?? throw new InvalidOperationException("Authenticated user required");
    public bool Admin => actor.IsAdmin;
    public bool Staff => actor.IsStaff;
    public Guid TenantId => tenant.TenantId;
    public IQueryable<T> Own<T>() where T : Owned => tenant.Own<T>();
    public IQueryable<ServiceRequest> Requests => requests.Query();
    public async Task<ServiceRequest> Request(Guid id, CancellationToken ct) =>
        await Requests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "request_not_found");
    public async Task ActivePremises(Guid owner, Guid premises, CancellationToken ct) =>
        Guard.That(await leases.Contains(owner, premises, ct), "active_lease_required", 404);
}

using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Cube.Api.Infrastructure.Persistence;
using Cube.Api.Modules.Property;
using Cube.Api.Modules.Requests.Domain;

namespace Cube.Api.Modules.Requests.Features;

public sealed class CreateRequestHandler(ICurrentActor actor, TenantScope tenant, ActiveLeases leases, CubeDb db)
{
    public async Task<Outcome<ServiceRequest>> Handle(Guid categoryId, RequestContent content, CancellationToken ct)
    {
        if (actor.IsStaff) return Outcome<ServiceRequest>.Reject(Failure.Forbidden("tenant_required"));
        var tenantId = tenant.TenantId;
        var category = await db.Categories.FindAsync([categoryId], ct);
        if (category is null) return Outcome<ServiceRequest>.Reject(Failure.Invalid("invalid_category"));
        if (content.PremisesId is Guid premises && !await leases.Contains(tenantId, premises, ct))
            return Outcome<ServiceRequest>.Reject(Failure.NotFound("active_lease_required"));

        var result = RequestFactory.Create(content, tenantId, category);
        if (result.Error is not null) return result;
        var request = result.Value;
        db.Requests.Add(request);
        Events.Queue(db, "Bitrix", "RequestCreated", request.Id);
        await Events.NotifyStaffAsync(db, request.AssigneeId, request.TenantId, "StaffRequestAssigned", request.Id, "Вам назначено обращение: " + request.Subject, ct);
        await db.SaveChangesAsync(ct);
        return result;
    }
}

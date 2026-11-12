using Cube.Api.Common.Identity;
using Cube.Api.Infrastructure.Persistence;

namespace Cube.Api.Modules.Requests;

public sealed class RequestVisibility(ICurrentActor actor, CubeDb db, TenantScope tenant)
{
    public IQueryable<ServiceRequest> Query() => actor.IsAdmin ? db.Requests
        : actor.IsStaff ? db.Requests.Where(x => x.AssigneeId == actor.UserId)
        : tenant.Own<ServiceRequest>();
}

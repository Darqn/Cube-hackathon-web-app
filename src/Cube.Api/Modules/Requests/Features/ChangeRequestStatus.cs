using Cube.Api.Modules.Requests.Domain;
using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Requests.Features;

public sealed class ChangeRequestStatusHandler(ICurrentActor actor, RequestVisibility visibility, CubeDb db)
{
    public async Task<Outcome<ServiceRequest>> Handle(Guid id, string status, string reason, uint version, CancellationToken ct)
    {
        if (!actor.IsStaff) return Outcome<ServiceRequest>.Reject(Failure.Forbidden("staff_required"));
        var request = await visibility.Query().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Outcome<ServiceRequest>.Reject(Failure.NotFound("request_not_found"));
        if (request.Version != version) return Outcome<ServiceRequest>.Reject(Failure.Conflict("stale_version"));
        if (!RequestRules.CanTransition(request.Status, status)) return Outcome<ServiceRequest>.Reject(Failure.Conflict("invalid_transition"));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) return Outcome<ServiceRequest>.Reject(Failure.Invalid("invalid_reason"));

        var changed = new RequestEvent
        {
            TenantId = request.TenantId, RequestId = id, ActorId = actor.UserId!.Value,
            FromStatus = request.Status, ToStatus = status, Reason = reason.Trim()
        };
        db.RequestEvents.Add(changed);
        request.Status = status;
        if (request.AssigneeId != actor.UserId)
            await Events.NotifyStaffAsync(db, request.AssigneeId, request.TenantId, "StaffRequestStatus", id, "Изменён статус обращения: " + request.Subject, ct);
        Events.Queue(db, "Bitrix", "RequestStatusChanged", changed.Id);
        await Events.NotifyAsync(db, request.TenantId, "RequestStatus", id, "Your request status changed", ct);
        await db.SaveChangesAsync(ct);
        return Outcome<ServiceRequest>.Success(request);
    }
}

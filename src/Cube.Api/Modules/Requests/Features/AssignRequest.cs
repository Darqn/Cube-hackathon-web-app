using Cube.Api.Common.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Requests.Features;

public sealed class AssignRequestHandler(CubeDb db)
{
    public async Task<Outcome<ServiceRequest>> Handle(Guid id, Guid? assigneeId, DateTimeOffset? dueAt, uint version, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([id], ct);
        if (request is null)
            return Outcome<ServiceRequest>.Reject(Failure.NotFound("request_not_found"));
        if (request.Version != version)
            return Outcome<ServiceRequest>.Reject(Failure.Conflict("stale_version"));
        if (assigneeId is Guid user && !await db.Users.AnyAsync(x => x.Id == user && (x.Role == "Staff" || x.Role == "Admin") && !x.Blocked && x.Approved, ct))
            return Outcome<ServiceRequest>.Reject(Failure.Invalid("invalid_assignee"));

        var reassigned = request.AssigneeId != assigneeId;
        var deadlineChanged = request.DueAt != dueAt;
        request.AssigneeId = assigneeId;
        request.DueAt = dueAt;
        if (reassigned || deadlineChanged)
            await Events.NotifyStaffAsync(db, assigneeId, request.TenantId, reassigned ? "StaffRequestAssigned" : "StaffRequestDeadline", id,
                (reassigned ? "Вам назначено обращение: " : "Изменён срок обращения: ") + request.Subject, ct);
        await db.SaveChangesAsync(ct);
        return Outcome<ServiceRequest>.Success(request);
    }
}

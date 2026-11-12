using Cube.Api.Common.Identity;
using Cube.Api.Modules.Metering;
using Cube.Api.Modules.Requests;

namespace Cube.Api.Modules.Notifications;

public sealed class StaffNotificationVisibility(ICurrentActor actor, CubeDb db, RequestVisibility requests, MeteringVisibility metering)
{
    public IQueryable<Notification> Query()
    {
        var visibleRequests = requests.Query();
        var visibleReadings = metering.Readings();
        return db.Notifications.Where(n => n.UserId == actor.UserId && n.ResourceId != null &&
            ((n.Kind.StartsWith("StaffRequest") && visibleRequests.Any(r => r.Id == n.ResourceId && r.TenantId == n.TenantId)) ||
             (n.Kind == "StaffReadingSubmitted" && visibleReadings.Any(r => r.Id == n.ResourceId && r.TenantId == n.TenantId))));
    }
}

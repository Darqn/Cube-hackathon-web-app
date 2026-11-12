using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class Events
{
    public static void Queue(CubeDb db, string provider, string kind, Guid id) => db.Outbox.Add(new Outbox { Provider = provider, Kind = kind, ResourceId = id });
    public static async Task NotifyAsync(CubeDb db, Guid tenant, string kind, Guid id, string text, CancellationToken ct)
    {
        foreach (var userId in await db.Users.Where(x => x.TenantId == tenant && x.Approved && !x.Blocked).Select(x => x.Id).ToListAsync(ct))
            db.Notifications.Add(new Notification { TenantId = tenant, UserId = userId, Kind = kind, ResourceId = id, Text = text });
    }
    public static async Task NotifyStaffAsync(CubeDb db, Guid? user, Guid tenant, string kind, Guid resource, string text, CancellationToken ct)
    {
        if (user is Guid id && await db.Users.AnyAsync(u => u.Id == id && u.Approved && !u.Blocked && (u.Role == "Staff" || u.Role == "Admin"), ct))
            db.Notifications.Add(new Notification { TenantId = tenant, UserId = id, Kind = kind, ResourceId = resource, Text = text });
    }
    public static async Task NotifyReadingStaffAsync(CubeDb db, Meter meter, Reading reading, CancellationToken ct)
    {
        var code = meter.Type == "Electricity" ? "electricity" : "water-heating";
        var user = await db.Categories.Where(c => c.Code == code).Select(c => c.ResponsibleUserId).FirstOrDefaultAsync(ct);
        await NotifyStaffAsync(db, user, meter.TenantId, "StaffReadingSubmitted", reading.Id, "Поступили новые показания счётчика " + meter.SerialNumber, ct);
    }
}

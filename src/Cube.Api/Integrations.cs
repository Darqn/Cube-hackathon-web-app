namespace Cube.Api;

// Contracts intentionally describe local business events, not a fabricated remote API.
// Implement a concrete provider after its documentation/authentication is available.
public record DeliveryResult(bool Delivered, string State, string? ExternalId = null);
public interface IBitrixGateway { Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken); }
public interface IAccountingGateway { Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken); }
public interface IPaymentGateway { Task<Uri> CreateCheckoutAsync(Invoice invoice, string idempotencyKey, CancellationToken cancellationToken); }
public interface IEmailGateway { Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken); }
public sealed class UnconfiguredIntegrations : IBitrixGateway, IAccountingGateway, IPaymentGateway, IEmailGateway
{
    public Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken) => Task.FromResult(new DeliveryResult(false, "AwaitingConfiguration"));
    public Task<Uri> CreateCheckoutAsync(Invoice invoice, string idempotencyKey, CancellationToken cancellationToken) => throw new ApiError(503, "payment_provider_not_configured");
}
public static class Events
{
    public static void Queue(CubeDb db, string provider, string kind, Guid id) => db.Outbox.Add(new Outbox { Provider = provider, Kind = kind, ResourceId = id });
    public static void Notify(CubeDb db, Guid tenant, string kind, Guid id, string text)
    {
        foreach (var userId in db.Users.Where(x => x.TenantId == tenant && x.Approved && !x.Blocked).Select(x => x.Id).ToList())
            db.Notifications.Add(new Notification { TenantId = tenant, UserId = userId, Kind = kind, ResourceId = id, Text = text });
    }
    public static void NotifyStaff(CubeDb db, Guid? user, Guid tenant, string kind, Guid resource, string text)
    {
        if (user is Guid id && db.Users.Any(u => u.Id == id && u.Approved && !u.Blocked && (u.Role == "Staff" || u.Role == "Admin")))
            db.Notifications.Add(new Notification { TenantId = tenant, UserId = id, Kind = kind, ResourceId = resource, Text = text });
    }
    public static void NotifyReadingStaff(CubeDb db, Meter meter, Reading reading)
    {
        var code = meter.Type == "Electricity" ? "electricity" : "water-heating";
        var user = db.Categories.Where(c => c.Code == code).Select(c => c.ResponsibleUserId).FirstOrDefault();
        NotifyStaff(db, user, meter.TenantId, "StaffReadingSubmitted", reading.Id, "Поступили новые показания счётчика " + meter.SerialNumber);
    }
}

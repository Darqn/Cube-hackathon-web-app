using Microsoft.AspNetCore.Identity;
namespace Cube.Api;

public sealed class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";
    public Guid? TenantId { get; set; }
    public bool Approved { get; set; }
    public bool Blocked { get; set; }
    public string Role { get; set; } = "Tenant";
    public bool InvoiceReminders { get; set; } = true;
    public bool MeterReminders { get; set; } = true;
    public bool NewsNotifications { get; set; } = true;
    public string RequestedOrganization { get; set; } = "";
    public string RequestedInn { get; set; } = "";
}
public abstract class Entity { public Guid Id { get; set; } = Guid.NewGuid(); public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public abstract class Owned : Entity { public Guid TenantId { get; set; } }
public sealed class Tenant : Entity { public string Name { get; set; } = ""; public string Inn { get; set; } = ""; public string ContactName { get; set; } = ""; public string Email { get; set; } = ""; public string Phone { get; set; } = ""; public bool Blocked { get; set; } }
public sealed class Session : Entity { public Guid UserId { get; set; } public string TokenHash { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; } public string SecurityStamp { get; set; } = ""; }
public sealed class Premises : Entity
{
    public string Name { get; set; } = ""; public string Type { get; set; } = "Office"; public string Address { get; set; } = "";
    public string Building { get; set; } = ""; public string Section { get; set; } = ""; public int? Floor { get; set; }
    public decimal Area { get; set; }
    public decimal MonthlyRent { get; set; }
    public string Description { get; set; } = "";
    public string Utilities { get; set; } = ""; public string Status { get; set; } = "Available";
}
public sealed class Lease : Owned
{
    public Guid PremisesId { get; set; }
    public string Number { get; set; } = ""; public DateOnly SignedOn { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Status { get; set; } = "Active";
}
public sealed class Invoice : Owned
{
    public string ExternalId { get; set; } = ""; public string Number { get; set; } = ""; public string Kind { get; set; } = "Rent";
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public string Purpose { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public string Currency { get; set; } = "RUB"; public Guid? FileId { get; set; }
}
public sealed class Category : Entity { public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string Department { get; set; } = ""; public Guid? ResponsibleUserId { get; set; } }
public sealed class ServiceRequest : Owned
{
    public Guid CategoryId { get; set; }
    public Guid? PremisesId { get; set; }
    public Guid? AssigneeId { get; set; }
    public string Subject { get; set; } = ""; public string Description { get; set; } = ""; public string ContactName { get; set; } = "";
    public string Phone { get; set; } = ""; public string Email { get; set; } = ""; public string Status { get; set; } = "New";
    public DateTimeOffset? DueAt { get; set; }
    public uint Version { get; set; }
}
public sealed class RequestMessage : Owned { public Guid RequestId { get; set; } public Guid AuthorId { get; set; } public string Text { get; set; } = ""; public bool Internal { get; set; } }
public sealed class RequestEvent : Owned { public Guid RequestId { get; set; } public Guid ActorId { get; set; } public string FromStatus { get; set; } = ""; public string ToStatus { get; set; } = ""; public string Reason { get; set; } = ""; }
public sealed class Meter : Owned { public Guid PremisesId { get; set; } public string Type { get; set; } = "Electricity"; public string SerialNumber { get; set; } = ""; public string Unit { get; set; } = "kWh"; public decimal LastValue { get; set; } public bool Active { get; set; } = true; public uint Version { get; set; } }
public sealed class Reading : Owned { public Guid MeterId { get; set; } public decimal PreviousValue { get; set; } public decimal Value { get; set; } public Guid? PhotoId { get; set; } public string Status { get; set; } = "Submitted"; public string? ReviewReason { get; set; } }
public sealed class StoredFile : Owned { public Guid UploadedBy { get; set; } public string Key { get; set; } = ""; public string Name { get; set; } = ""; public string ContentType { get; set; } = ""; public long Size { get; set; } public string State { get; set; } = "Quarantined"; }
public sealed class Document : Owned { public Guid FileId { get; set; } public string Title { get; set; } = ""; public string Kind { get; set; } = "Contract"; public string Number { get; set; } = ""; public DateOnly DocumentDate { get; set; } public Guid? LeaseId { get; set; } public Guid? PremisesId { get; set; } public bool Archived { get; set; } public string Status { get; set; } = "Published"; }
public sealed class Attachment : Owned { public Guid FileId { get; set; } public Guid? RequestId { get; set; } public Guid? MessageId { get; set; } }
public sealed class Listing : Entity { public Guid PremisesId { get; set; } public string Title { get; set; } = ""; public string Description { get; set; } = ""; public bool Published { get; set; } public bool Archived { get; set; } }
public sealed class ListingInquiry : Owned { public Guid ListingId { get; set; } public Guid RequestId { get; set; } }
public sealed class News : Entity
{
    public string Title { get; set; } = ""; public string Summary { get; set; } = ""; public string Body { get; set; } = "";
    public string Category { get; set; } = "General"; public string Importance { get; set; } = "Normal";
    public DateTimeOffset? PublishAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool Archived { get; set; }
    public bool AllTenants { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public Guid[] TenantIds { get; set; } = []; public Guid[] PremisesIds { get; set; } = [];
    public string? Building { get; set; }
    public string? Section { get; set; }
    public int? Floor { get; set; }
}
public sealed class NewsReceipt : Owned { public Guid NewsId { get; set; } public Guid UserId { get; set; } public DateTimeOffset? ReadAt { get; set; } public DateTimeOffset? AcknowledgedAt { get; set; } }
public sealed class Notification : Owned { public string? DeduplicationKey { get; set; } public string Kind { get; set; } = ""; public string Text { get; set; } = ""; public Guid? ResourceId { get; set; } public Guid? UserId { get; set; } public bool Read { get; set; } }
public sealed class Outbox : Entity { public string Provider { get; set; } = ""; public string Kind { get; set; } = ""; public Guid ResourceId { get; set; } public string Status { get; set; } = "AwaitingConfiguration"; public string? LastError { get; set; } public int Attempts { get; set; } }
public sealed class Audit : Entity { public Guid ActorId { get; set; } public string Action { get; set; } = ""; public Guid ResourceId { get; set; } }

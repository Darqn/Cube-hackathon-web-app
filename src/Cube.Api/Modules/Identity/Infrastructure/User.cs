using Microsoft.AspNetCore.Identity;
namespace Cube.Api;

public sealed class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";
    public Guid? TenantId
    {
        get; set;
    }
    public bool Approved
    {
        get; set;
    }
    public bool Blocked
    {
        get; set;
    }
    public string Role { get; set; } = "Tenant";
    public bool InvoiceReminders { get; set; } = true;
    public bool MeterReminders { get; set; } = true;
    public bool NewsNotifications { get; set; } = true;
    public string RequestedOrganization { get; set; } = "";
    public string RequestedInn { get; set; } = "";
}
public sealed class Session : Entity
{
    public Guid UserId
    {
        get; set;
    }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt
    {
        get; set;
    }
    public string SecurityStamp { get; set; } = "";
}

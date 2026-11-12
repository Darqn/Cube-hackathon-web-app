namespace Cube.Api.Common.Identity;

// Resolved per scope; values stay live while authentication establishes the principal.
public interface ICurrentActor
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    bool IsAdmin { get; }
    bool IsStaff { get; }
}

using System.Security.Claims;
using Cube.Api.Common.Identity;

namespace Cube.Api.Api.Authentication;

internal sealed class HttpCurrentActor(IHttpContextAccessor http) : ICurrentActor
{
    public Guid? UserId => ParseClaim(ClaimTypes.NameIdentifier);
    public Guid? TenantId => ParseClaim("tenant");
    public bool IsAdmin => http.HttpContext?.User.IsInRole("Admin") == true;
    public bool IsStaff => IsAdmin || http.HttpContext?.User.IsInRole("Staff") == true;

    private Guid? ParseClaim(string name) =>
        Guid.TryParse(http.HttpContext?.User.FindFirstValue(name), out var id) ? id : null;
}

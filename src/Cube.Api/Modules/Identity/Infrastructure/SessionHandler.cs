using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cube.Api;

public sealed class SessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, CubeDb db, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var token = header[7..]; if (token.Length != 64) return AuthenticateResult.Fail("Invalid session");
        var hash = Tokens.Hash(token);
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > clock.GetUtcNow(), Context.RequestAborted);
        if (session is null) return AuthenticateResult.Fail("Invalid session");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.UserId, Context.RequestAborted);
        if (user is null || !user.Approved || user.Blocked || user.SecurityStamp != session.SecurityStamp) return AuthenticateResult.Fail("Invalid session");
        if (user.TenantId is Guid tid && !await db.Tenants.AnyAsync(x => x.Id == tid && !x.Blocked, Context.RequestAborted)) return AuthenticateResult.Fail("Invalid session");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role), new(ClaimTypes.Name, user.UserName ?? "") };
        if (user.TenantId is Guid t) claims.Add(new("tenant", t.ToString()));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}

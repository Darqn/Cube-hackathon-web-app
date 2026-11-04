using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Cube.Api;

public sealed class ApiError(int status, string code) : Exception(code) { public int Status { get; } = status; }
public static class Guard
{
    public static void That(bool condition, string code, int status = 400) { if (!condition) throw new ApiError(status, code); }
    public static string Text(string? value, string name, int max = 200) { That(!string.IsNullOrWhiteSpace(value) && value.Length <= max, $"invalid_{name}"); return value!.Trim(); }
    public static string Email(string value) { That(System.Net.Mail.MailAddress.TryCreate(value, out var mail) && mail.Address == value && value.Length <= 254, "invalid_email"); return value; }
    public static int Page(int? page) => Math.Clamp(page ?? 1, 1, 100000);
    public static int Size(int? size) => Math.Clamp(size ?? 25, 1, 100);
}
public static class Tokens
{
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
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
public sealed class Access(IHttpContextAccessor http, CubeDb db)
{
    public Guid UserId => Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public bool Admin => http.HttpContext!.User.IsInRole("Admin");
    public bool Staff => Admin || http.HttpContext!.User.IsInRole("Staff");
    public Guid TenantId => Guid.TryParse(http.HttpContext!.User.FindFirstValue("tenant"), out var t) ? t : throw new ApiError(403, "tenant_context_required");
    public IQueryable<T> Own<T>() where T : Owned => db.Set<T>().Where(x => x.TenantId == TenantId);
    public IQueryable<ServiceRequest> Requests => Admin ? db.Requests : Staff ? db.Requests.Where(x => x.AssigneeId == UserId) : Own<ServiceRequest>();
    public async Task<ServiceRequest> Request(Guid id, CancellationToken ct) => await Requests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "request_not_found");
    public async Task ActivePremises(Guid tenant, Guid premises, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guard.That(await db.Leases.AnyAsync(x => x.TenantId == tenant && x.PremisesId == premises && x.Status == "Active" && x.StartsOn <= today && x.EndsOn >= today, ct), "active_lease_required", 404);
    }
}
public static class AuthEndpoints
{
    public record Register(string Login, string Password, string Email, string FullName, string Organization, string Inn, string Phone);
    public record Login(string LoginName, string Password);
    public record ChangePassword(string CurrentPassword, string NewPassword);
    public record ProfileInput(string FullName, string Phone);
    public record Preferences(bool InvoiceReminders, bool MeterReminders, bool NewsNotifications);
    public record ResetRequest(string Email);
    public record ResetPassword(string Email, string Token, string NewPassword);
    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/auth").AddEndpointFilter<InputValidationFilter>().WithTags("Authentication").RequireRateLimiting("auth");
        g.MapPost("/register", async (Register input, UserManager<User> users, CubeDb db, CancellationToken ct) =>
        {
            Guard.Email(input.Email); Guard.That(Rules.ValidInn(input.Inn), "invalid_inn");
            var u = new User { Id = Guid.NewGuid(), UserName = Guard.Text(input.Login, "login", 100), Email = input.Email, FullName = Guard.Text(input.FullName, "name"), RequestedOrganization = Guard.Text(input.Organization, "organization"), RequestedInn = input.Inn, PhoneNumber = Guard.Text(input.Phone, "phone", 40) };
            var result = await users.CreateAsync(u, input.Password);
            Guard.That(result.Succeeded, "registration_invalid_or_duplicate");
            return Results.Accepted(value: new { Status = "PendingApproval" });
        });
        g.MapPost("/login", async (Login input, UserManager<User> users, CubeDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var u = await users.FindByNameAsync(input.LoginName);
            // A fixed valid hash performs password work for unknown users to reduce timing disclosure.
            if (u is null) { new PasswordHasher<User>().VerifyHashedPassword(new User(), DummyHash.Value, input.Password); throw new ApiError(401, "invalid_credentials"); }
            Guard.That(!await users.IsLockedOutAsync(u), "invalid_credentials", 401);
            if (!await users.CheckPasswordAsync(u, input.Password)) { await users.AccessFailedAsync(u); throw new ApiError(401, "invalid_credentials"); }
            Guard.That(u.Approved && !u.Blocked && (u.TenantId is null || await db.Tenants.AnyAsync(t => t.Id == u.TenantId && !t.Blocked, ct)), "invalid_credentials", 401);
            await users.ResetAccessFailedCountAsync(u);
            var raw = Tokens.New(); var expiry = clock.GetUtcNow().AddHours(8);
            db.Sessions.Add(new Session { UserId = u.Id, TokenHash = Tokens.Hash(raw), ExpiresAt = expiry, SecurityStamp = u.SecurityStamp! }); await db.SaveChangesAsync(ct);
            return Results.Ok(new { AccessToken = raw, TokenType = "Bearer", ExpiresAt = expiry });
        });
        g.MapPost("/logout", async (HttpContext context, CubeDb db, CancellationToken ct) => { var hash = Tokens.Hash(context.Request.Headers.Authorization.ToString()[7..]); await db.Sessions.Where(x => x.TokenHash == hash).ExecuteDeleteAsync(ct); return Results.NoContent(); }).RequireAuthorization();
        g.MapGet("/me", async (Access a, CubeDb db, CancellationToken ct) => await db.Users.Where(x => x.Id == a.UserId).Select(x => new { x.Id, x.UserName, x.Email, x.FullName, x.PhoneNumber, x.TenantId, x.Role }).SingleAsync(ct)).RequireAuthorization();
        g.MapPut("/profile", async (ProfileInput i, Access a, UserManager<User> users) => { var u = (await users.FindByIdAsync(a.UserId.ToString()))!; u.FullName = Guard.Text(i.FullName, "name"); u.PhoneNumber = Guard.Text(i.Phone, "phone", 40); Guard.That((await users.UpdateAsync(u)).Succeeded, "profile_update_failed", 409); return Results.NoContent(); }).RequireAuthorization();
        g.MapGet("/notification-preferences", async (Access a, CubeDb db, CancellationToken ct) => await db.Users.Where(x => x.Id == a.UserId).Select(x => new { x.InvoiceReminders, x.MeterReminders, x.NewsNotifications }).SingleAsync(ct)).RequireAuthorization();
        g.MapPut("/notification-preferences", async (Preferences i, Access a, UserManager<User> users) => { var u = (await users.FindByIdAsync(a.UserId.ToString()))!; u.InvoiceReminders = i.InvoiceReminders; u.MeterReminders = i.MeterReminders; u.NewsNotifications = i.NewsNotifications; Guard.That((await users.UpdateAsync(u)).Succeeded, "preferences_update_failed", 409); return Results.NoContent(); }).RequireAuthorization();
        g.MapPost("/change-password", async (ChangePassword input, Access a, UserManager<User> users, CubeDb db, CancellationToken ct) =>
        {
            var u = (await users.FindByIdAsync(a.UserId.ToString()))!; var result = await users.ChangePasswordAsync(u, input.CurrentPassword, input.NewPassword); Guard.That(result.Succeeded, "password_change_failed");
            await db.Sessions.Where(x => x.UserId == u.Id).ExecuteDeleteAsync(ct); return Results.NoContent();
        }).RequireAuthorization();
        g.MapPost("/forgot-password", async (ResetRequest input, UserManager<User> users, CubeDb db, CancellationToken ct) =>
        {
            Guard.Email(input.Email); var u = await users.FindByEmailAsync(input.Email);
            if (u is { Approved: true, Blocked: false }) { if (!await db.Outbox.AnyAsync(x => x.Provider == "Email" && x.Kind == "PasswordRecovery" && x.ResourceId == u.Id, ct)) { db.Outbox.Add(new Outbox { Provider = "Email", Kind = "PasswordRecovery", ResourceId = u.Id }); await db.SaveChangesAsync(ct); } }
            // Delivery adapter must generate a fresh Identity token when sending; never persist a raw reset token.
            return Results.Accepted(value: new { Message = "If the account is eligible, recovery is queued. Email delivery is not configured." });
        });
        g.MapPost("/reset-password", async (ResetPassword input, UserManager<User> users, CubeDb db, CancellationToken ct) =>
        {
            var u = await users.FindByEmailAsync(input.Email); Guard.That(u is { Approved: true, Blocked: false }, "reset_failed");
            var result = await users.ResetPasswordAsync(u!, input.Token, input.NewPassword); Guard.That(result.Succeeded, "reset_failed");
            await db.Sessions.Where(x => x.UserId == u!.Id).ExecuteDeleteAsync(ct); return Results.NoContent();
        });
    }
    private static class DummyHash { public static readonly string Value = new PasswordHasher<User>().HashPassword(new User(), "not-a-user-password"); }
}

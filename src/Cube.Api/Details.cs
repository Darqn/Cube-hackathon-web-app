using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class DetailEndpoints
{
    public static void MapDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/me/leases/{id:guid}", async (Guid id, Access a, CubeDb db, CancellationToken ct) => await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id where l.Id == id select new { Lease = l, Premises = p }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound());
        g.MapGet("/me/premises/{id:guid}", async (Guid id, Access a, CubeDb db, CancellationToken ct) => { var today = DateOnly.FromDateTime(DateTime.UtcNow); return await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id where p.Id == id && l.Status == "Active" && l.StartsOn <= today && l.EndsOn >= today select new { Lease = l, Premises = p }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound(); });
        g.MapGet("/meters/{id:guid}", async (Guid id, Access a, CancellationToken ct) => await a.Own<Meter>().SingleOrDefaultAsync(x => x.Id == id, ct) is { } item ? Results.Ok(item) : Results.NotFound());
        g.MapGet("/admin/premises/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Premises.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");
        g.MapGet("/admin/users/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Users.Where(x => x.Id == id).Select(x => new { x.Id, x.UserName, x.Email, x.FullName, x.PhoneNumber, x.TenantId, x.Role, x.Approved, x.Blocked }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");
        g.MapGet("/admin/documents/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Documents.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");
        g.MapGet("/admin/invoices/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Invoices.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");
        g.MapGet("/admin/news/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.News.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");
    }
}

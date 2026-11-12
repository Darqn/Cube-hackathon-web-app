using Cube.Api.Modules.News;
using Microsoft.EntityFrameworkCore;
using static Cube.Api.TenantEndpoints;
using Cube.Api.Workflows;
using Cube.Api.Modules.Billing.Domain;

namespace Cube.Api;

public static class DashboardTenantEndpoints
{
    public static void MapTenantDashboard(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Tenant portal");
        g.MapGet("/dashboard", async (Access a, CubeDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime); var invoices = await a.Own<Invoice>().Where(x => x.Paid < x.Amount).ToListAsync(ct);
            var visibleNews = await NewsAudience.ForTenant(a.TenantId, db, clock).OrderByDescending(x => x.PublishAt).Take(100).ToListAsync(ct);
            return Results.Ok(new { Organization = await db.Tenants.Where(x => x.Id == a.TenantId).Select(x => new { x.Name, x.Inn }).SingleAsync(ct), ActiveLeases = await a.Own<Lease>().CountAsync(x => x.Status == "Active" && x.StartsOn <= today && x.EndsOn >= today, ct), OutstandingAmount = invoices.Sum(x => x.Amount - x.Paid), OverdueAmount = invoices.Where(x => x.DueOn < today).Sum(x => x.Amount - x.Paid), NextPayment = invoices.Where(x => x.DueOn >= today).OrderBy(x => x.DueOn).Select(x => new { x.Id, x.DueOn, Balance = x.Amount - x.Paid }).FirstOrDefault(), UnreadNotifications = await a.Own<Notification>().CountAsync(x => !x.Read && (x.UserId == null || x.UserId == a.UserId), ct), ImportantNews = visibleNews.Where(x => x.Importance != "Normal").OrderByDescending(x => x.Importance == "Urgent").Take(10).Select(NewsResponses.Dto) });
        });

    }
}

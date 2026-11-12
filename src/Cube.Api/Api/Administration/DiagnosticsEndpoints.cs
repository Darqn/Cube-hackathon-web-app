using Cube.Api.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Cube.Api.AdminEndpoints;

namespace Cube.Api;

public static class DiagnosticsAdminEndpoints
{
    public static void MapAdminDiagnostics(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").AddEndpointFilter<InputValidationFilter>().RequireAuthorization("Admin").WithTags("Administration");
        g.MapGet("/audit", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Audit.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/integrations", () => new { Accounting = "NotConfigured", Bitrix = "NotConfigured", Payments = "NotConfigured", Email = "NotConfigured", AutomaticDelivery = false });
        g.MapGet("/integration-handoffs", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Outbox.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
    }
}

using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class HealthEndpoints
{
    public static void MapHealth(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { Status = "Alive" }));
        app.MapGet("/health/ready", async (CubeDb db, CancellationToken ct) => { try { return await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any() ? Results.Ok(new { Status = "Ready" }) : Results.StatusCode(503); } catch { return Results.StatusCode(503); } });
    }
}

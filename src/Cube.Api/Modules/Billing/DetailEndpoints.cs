using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class BillingDetailEndpoints
{
    public static void MapBillingDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/admin/invoices/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Invoices.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");

    }
}

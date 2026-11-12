using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class NewsDetailEndpoints
{
    public static void MapNewsDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/admin/news/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.News.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");
    }
}

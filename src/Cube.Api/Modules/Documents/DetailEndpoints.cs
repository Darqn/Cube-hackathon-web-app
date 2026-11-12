using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class DocumentsDetailEndpoints
{
    public static void MapDocumentsDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/admin/documents/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Documents.FindAsync([id], ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireAuthorization("Admin");

    }
}

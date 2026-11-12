using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class MeteringDetailEndpoints
{
    public static void MapMeteringDetails(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Resource details");
        g.MapGet("/meters/{id:guid}", async (Guid id, Access a, CancellationToken ct) => await a.Own<Meter>().SingleOrDefaultAsync(x => x.Id == id, ct) is { } item ? Results.Ok(item) : Results.NotFound());

    }
}

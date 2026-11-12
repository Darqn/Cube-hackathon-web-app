using Cube.Api.Modules.Metering.Infrastructure;

namespace Cube.Api;

public static class MeterOcrEndpoints
{
    public const string Failure = "Не удалось уверенно распознать. Введите показание вручную";

    public static void MapMeterOcr(this WebApplication app)
    {
        app.MapPost("/api/v1/meters/recognize", async (HttpRequest request, MeterOcr ocr, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { message = Failure });
            var form = await request.ReadFormAsync(ct);
            if (form.Files.Count != 1) return Results.BadRequest(new { message = Failure });
            var file = form.Files[0];
            await using var image = file.OpenReadStream();
            var value = await ocr.Recognize(image, file.Length, file.ContentType, ct);
            return Results.Ok(new { value, message = value is null ? Failure : (string?)null });
        }).RequireAuthorization().WithTags("Meter OCR");
    }
}

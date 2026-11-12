using Cube.Api.Api.Files;
using Cube.Api.Infrastructure.Storage;
using Cube.Api.Modules.Requests.Features;

namespace Cube.Api;

public static class RequestReplyEndpoint
{
    public static void MapRequestReplies(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/staff").AddEndpointFilter<InputValidationFilter>()
            .RequireAuthorization(p => p.RequireRole("Staff", "Admin")).WithTags("Staff workspace");
        group.MapPost("/requests/{id:guid}/replies", Reply).RequireRateLimiting("auth");
    }

    private static async Task<IResult> Reply(Guid id, HttpRequest request, ReplyToRequestHandler handler, HttpContext context, CancellationToken ct)
    {
        var open = await handler.FindOpenRequest(id, ct);
        if (open.Error is not null)
            return open.ToHttpResult(context, Results.Ok);
        Guard.That(request.HasFormContentType, "multipart_required");
        var form = await request.ReadFormAsync(ct);
        Guard.That(bool.TryParse(form["internal"], out var internalNote), "invalid_message_visibility");
        var text = form["text"].ToString().Trim();
        Guard.That(text.Length <= 10000 && (text.Length > 0 || form.Files.Count > 0), "message_required");
        Guard.That(form.Files.Count <= 5 && form.Files.Sum(f => f.Length) <= 20 * 1024 * 1024, "invalid_file_size", 413);
        var uploads = new List<ValidatedUpload>();
        foreach (var upload in form.Files)
            uploads.Add(await UploadedFileReader.ReadAsync(upload, ct));
        var result = await handler.Handle(open.Value, text, internalNote, uploads, ct);
        return result.ToHttpResult(context, reply => Results.Created($"/api/v1/requests/{id}", new { reply.Id, reply.CreatedAt, reply.PendingFiles }));
    }
}

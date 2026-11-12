using Cube.Api.Modules.Requests.Features;

namespace Cube.Api;

public static class RequestAttachmentEndpoints
{
    public static void MapRequestAttachments(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Files and documents");
        group.MapPost("/requests/{id:guid}/attachments", async (Guid id, FileEndpoints.AttachmentInput input, AddRequestAttachmentHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.Handle(id, input.FileId, ct)).ToHttpResult(context, attachment =>
                Results.Created($"/api/v1/requests/{id}", new { attachment.Id, attachment.FileId })));
    }
}

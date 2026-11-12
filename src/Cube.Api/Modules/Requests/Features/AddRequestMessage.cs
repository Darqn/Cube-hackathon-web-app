using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Requests.Features;

public sealed class AddRequestMessageHandler(ICurrentActor actor, RequestVisibility visibility, CubeDb db)
{
    public async Task<Outcome<RequestMessage>> Handle(Guid id, string text, bool internalNote, CancellationToken ct)
    {
        var request = await visibility.Query().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return Outcome<RequestMessage>.Reject(Failure.NotFound("request_not_found"));
        if (internalNote && !actor.IsStaff)
            return Outcome<RequestMessage>.Reject(Failure.Forbidden("internal_comment_forbidden"));
        if (request.Status is "Closed" or "Rejected")
            return Outcome<RequestMessage>.Reject(Failure.Conflict("request_closed"));
        if (string.IsNullOrWhiteSpace(text) || text.Length > 10000)
            return Outcome<RequestMessage>.Reject(Failure.Invalid("invalid_message"));

        var message = new RequestMessage { TenantId = request.TenantId, RequestId = id, AuthorId = actor.UserId!.Value, Text = text.Trim(), Internal = internalNote };
        db.Messages.Add(message);
        if (request.AssigneeId != actor.UserId)
            await Events.NotifyStaffAsync(db, request.AssigneeId, request.TenantId, internalNote ? "StaffRequestInternalComment" : "StaffRequestMessage", id,
                (internalNote ? "Новая внутренняя заметка: " : "Новое сообщение: ") + request.Subject, ct);
        if (!internalNote)
        {
            Events.Queue(db, "Bitrix", "MessageCreated", message.Id);
            if (actor.IsStaff)
                await Events.NotifyAsync(db, request.TenantId, "RequestMessage", id, "New response on your request", ct);
        }
        await db.SaveChangesAsync(ct);
        return Outcome<RequestMessage>.Success(message);
    }
}

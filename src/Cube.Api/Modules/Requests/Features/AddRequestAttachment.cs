using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Requests.Features;

public sealed class AddRequestAttachmentHandler(ICurrentActor actor, RequestVisibility visibility, CubeDb db)
{
    public async Task<Outcome<Attachment>> Handle(Guid id, Guid fileId, CancellationToken ct)
    {
        var request = await visibility.Query().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Outcome<Attachment>.Reject(Failure.NotFound("request_not_found"));
        if (!actor.IsAdmin && actor.IsStaff) return Outcome<Attachment>.Reject(Failure.Forbidden("staff_upload_forbidden"));
        if (request.Status is "Closed" or "Rejected") return Outcome<Attachment>.Reject(Failure.Conflict("request_closed"));
        if (!await db.Files.AnyAsync(x => x.Id == fileId && x.TenantId == request.TenantId && x.State == "Ready", ct))
            return Outcome<Attachment>.Reject(Failure.NotFound("file_not_found"));

        var attachment = new Attachment { TenantId = request.TenantId, RequestId = id, FileId = fileId };
        db.Attachments.Add(attachment);
        Events.Queue(db, "Bitrix", "AttachmentAdded", attachment.Id);
        if (request.AssigneeId != actor.UserId)
            await Events.NotifyStaffAsync(db, request.AssigneeId, request.TenantId, "StaffRequestAttachment", id, "Добавлено вложение: " + request.Subject, ct);
        await db.SaveChangesAsync(ct);
        return Outcome<Attachment>.Success(attachment);
    }
}

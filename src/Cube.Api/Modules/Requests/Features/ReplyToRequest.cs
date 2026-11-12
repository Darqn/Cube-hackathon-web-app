using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Cube.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Requests.Features;

public sealed record ReplyReceipt(Guid Id, DateTimeOffset CreatedAt, int PendingFiles);

public sealed class ReplyToRequestHandler(ICurrentActor actor, RequestVisibility visibility, CubeDb db, PrivateStorage storage, FileWriteBatch files)
{
    // Resolve access before reading multipart content, preserving the HTTP operation's validation order.
    public async Task<Outcome<ServiceRequest>> FindOpenRequest(Guid id, CancellationToken ct)
    {
        var request = await visibility.Query().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Outcome<ServiceRequest>.Reject(Failure.NotFound("request_not_found"));
        if (request.Status is "Closed" or "Rejected") return Outcome<ServiceRequest>.Reject(Failure.Conflict("request_closed"));
        return Outcome<ServiceRequest>.Success(request);
    }

    public async Task<Outcome<ReplyReceipt>> Handle(ServiceRequest request, string text, bool internalNote, IReadOnlyList<ValidatedUpload> uploads, CancellationToken ct)
    {
        if (!actor.IsStaff) return Outcome<ReplyReceipt>.Reject(Failure.Forbidden("staff_required"));
        if (!actor.IsAdmin && request.AssigneeId != actor.UserId) return Outcome<ReplyReceipt>.Reject(Failure.NotFound("request_not_found"));
        if (request.Status is "Closed" or "Rejected") return Outcome<ReplyReceipt>.Reject(Failure.Conflict("request_closed"));
        text = text.Trim();
        if (text.Length > 10000 || (text.Length == 0 && uploads.Count == 0)) return Outcome<ReplyReceipt>.Reject(Failure.Invalid("message_required"));
        var message = new RequestMessage
        {
            TenantId = request.TenantId, RequestId = request.Id, AuthorId = actor.UserId!.Value,
            Internal = internalNote, Text = text.Length > 0 ? text : "Вложения к обращению."
        };
        db.Messages.Add(message);
        var writes = new List<FileWrite>();
        var pendingFiles = 0;
        foreach (var upload in uploads)
        {
            var file = new StoredFile
            {
                TenantId = request.TenantId, UploadedBy = actor.UserId.Value, Key = Guid.NewGuid().ToString("N"),
                Name = upload.Name, Size = upload.Bytes.Length, ContentType = upload.ContentType,
                State = storage.AllowDevelopmentFiles ? "Ready" : "Quarantined"
            };
            db.Files.Add(file);
            if (file.State != "Ready") pendingFiles++;
            db.Attachments.Add(new Attachment { TenantId = request.TenantId, RequestId = request.Id, MessageId = message.Id, FileId = file.Id });
            writes.Add(new FileWrite(file.Key, upload.Bytes));
        }
        if (!internalNote)
        {
            Events.Queue(db, "Bitrix", "MessageCreated", message.Id);
            await Events.NotifyAsync(db, request.TenantId, "RequestMessage", request.Id, "New response on your request", ct);
        }
        if (request.AssigneeId != actor.UserId)
            await Events.NotifyStaffAsync(db, request.AssigneeId, request.TenantId, internalNote ? "StaffRequestInternalComment" : "StaffRequestMessage", request.Id,
                "Новое сообщение в обращении: " + request.Subject, ct);
        await files.SaveAsync(writes, async token => { await db.SaveChangesAsync(token); }, ct);
        return Outcome<ReplyReceipt>.Success(new(message.Id, message.CreatedAt, pendingFiles));
    }
}

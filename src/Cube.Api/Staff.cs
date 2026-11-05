using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class StaffEndpoints
{
    public static IQueryable<Meter> Meters(Access a, CubeDb db) => db.Meters.Where(m => a.Admin || db.Categories.Any(c => c.ResponsibleUserId == a.UserId && ((c.Code == "electricity" && m.Type == "Electricity") || (c.Code == "water-heating" && m.Type == "Water"))));
    public static IQueryable<Reading> Readings(Access a, CubeDb db)
    {
        var ids = Meters(a, db).Select(m => m.Id); return db.Readings.Where(r => ids.Contains(r.MeterId));
    }
    public static IQueryable<Notification> Notifications(Access a, CubeDb db)
    {
        var requests = a.Requests; var readings = Readings(a, db);
        return db.Notifications.Where(n => n.UserId == a.UserId && n.ResourceId != null && ((n.Kind.StartsWith("StaffRequest") && requests.Any(r => r.Id == n.ResourceId && r.TenantId == n.TenantId)) || (n.Kind == "StaffReadingSubmitted" && readings.Any(r => r.Id == n.ResourceId && r.TenantId == n.TenantId))));
    }
    public static void MapStaff(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/staff").AddEndpointFilter<InputValidationFilter>().RequireAuthorization(p => p.RequireRole("Staff", "Admin")).WithTags("Staff workspace");
        g.MapPost("/requests/{id:guid}/replies", Reply).RequireRateLimiting("auth");
        g.MapGet("/dashboard", async (Access a, CubeDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow(); var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); var tomorrow = day.AddDays(1);
            var active = a.Requests.Where(r => r.Status != "Closed" && r.Status != "Rejected" && r.Status != "Completed");
            return Results.Ok(new
            {
                Assigned = await a.Requests.CountAsync(ct),
                Active = await active.CountAsync(ct),
                New = await active.CountAsync(r => r.Status == "New", ct),
                WaitingForTenant = await active.CountAsync(r => r.Status == "Clarification", ct),
                Overdue = await active.CountAsync(r => r.DueAt != null && r.DueAt < now, ct),
                DueToday = await active.CountAsync(r => r.DueAt >= day && r.DueAt < tomorrow, ct),
                UnreadNotifications = await Notifications(a, db).CountAsync(n => !n.Read, ct),
                PendingReadings = await Readings(a, db).CountAsync(r => r.Status == "Submitted", ct),
                Directions = await db.Categories.Where(c => a.Admin || c.ResponsibleUserId == a.UserId).OrderBy(c => c.Code).Select(c => new { c.Id, c.Code, c.Name, c.Department }).ToListAsync(ct)
            });
        });
        g.MapGet("/notifications", async (Access a, CubeDb db, bool? unreadOnly, int? page, int? size, CancellationToken ct) =>
            await Notifications(a, db).AsNoTracking().Where(n => unreadOnly != true || !n.Read).OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/notifications/{id:guid}/read", async (Guid id, Access a, CubeDb db, CancellationToken ct) =>
        {
            var n = await Notifications(a, db).SingleOrDefaultAsync(n => n.Id == id, ct) ?? throw new ApiError(404, "notification_not_found"); n.Read = true; await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        g.MapPost("/notifications/read-all", async (Access a, CubeDb db, CancellationToken ct) =>
        {
            var changed = await Notifications(a, db).Where(n => !n.Read).ExecuteUpdateAsync(p => p.SetProperty(n => n.Read, true), ct);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = "ReadStaffNotifications", ResourceId = a.UserId }); await db.SaveChangesAsync(ct); return Results.Ok(new { Updated = changed });
        });
        g.MapPut("/readings/{id:guid}/review", ReviewReading);
        g.MapGet("/readings/{id:guid}/photo", async (Guid id, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct) =>
        {
            var r = await Readings(a, db).AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new ApiError(404, "reading_not_found");
            Guard.That(r.PhotoId.HasValue, "photo_not_available", 404);
            var file = await db.Files.SingleOrDefaultAsync(f => f.Id == r.PhotoId && f.TenantId == r.TenantId && f.State == "Ready", ct) ?? throw new ApiError(404, "photo_not_available");
            var path = storage.PathFor(file.Key); Guard.That(File.Exists(path), "photo_not_available", 404); return Results.File(path, file.ContentType, file.Name, enableRangeProcessing: true);
        });
    }
    private static async Task<IResult> Reply(Guid id, HttpRequest request, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct)
    {
        var r = await a.Request(id, ct); Guard.That(r.Status is not "Closed" and not "Rejected", "request_closed", 409);
        Guard.That(request.HasFormContentType, "multipart_required"); var form = await request.ReadFormAsync(ct);
        Guard.That(bool.TryParse(form["internal"], out var internalNote), "invalid_message_visibility");
        var text = form["text"].ToString().Trim(); Guard.That(text.Length <= 10000 && (text.Length > 0 || form.Files.Count > 0), "message_required");
        Guard.That(form.Files.Count <= 5 && form.Files.Sum(f => f.Length) <= 20 * 1024 * 1024, "invalid_file_size", 413);
        var uploads = new List<(StoredFile File, byte[] Bytes)>();
        foreach (var upload in form.Files)
        {
            Guard.That(upload.Length > 0, "invalid_file_size", 413); var name = Path.GetFileName(upload.FileName);
            Guard.That(name.Length is > 0 and <= 200 && !name.Any(char.IsControl), "invalid_filename");
            using var stream = new MemoryStream(); await upload.CopyToAsync(stream, ct); var bytes = stream.ToArray();
            uploads.Add((new StoredFile { TenantId = r.TenantId, UploadedBy = a.UserId, Key = Guid.NewGuid().ToString("N"), Name = name, Size = bytes.Length, ContentType = PrivateStorage.Validate(bytes, name), State = storage.AllowDevelopmentFiles ? "Ready" : "Quarantined" }, bytes));
        }
        var msg = new RequestMessage { TenantId = r.TenantId, RequestId = r.Id, AuthorId = a.UserId, Internal = internalNote, Text = text.Length > 0 ? text : "Вложения к обращению." };
        db.Messages.Add(msg);
        foreach (var upload in uploads) { db.Files.Add(upload.File); db.Attachments.Add(new Attachment { TenantId = r.TenantId, RequestId = r.Id, MessageId = msg.Id, FileId = upload.File.Id }); }
        if (!internalNote) { Events.Queue(db, "Bitrix", "MessageCreated", msg.Id); Events.Notify(db, r.TenantId, "RequestMessage", r.Id, "New response on your request"); }
        if (r.AssigneeId != a.UserId) Events.NotifyStaff(db, r.AssigneeId, r.TenantId, internalNote ? "StaffRequestInternalComment" : "StaffRequestMessage", r.Id, "Новое сообщение в обращении: " + r.Subject);
        var written = new List<string>();
        try
        {
            Directory.CreateDirectory(storage.Root);
            foreach (var upload in uploads) { var path = storage.PathFor(upload.File.Key); written.Add(path); await File.WriteAllBytesAsync(path, upload.Bytes, ct); }
            await db.SaveChangesAsync(ct);
        }
        catch { foreach (var path in written) if (File.Exists(path)) File.Delete(path); throw; }
        return Results.Created($"/api/v1/requests/{id}", new { msg.Id, msg.CreatedAt, PendingFiles = uploads.Count(u => u.File.State != "Ready") });
    }
    public static async Task<IResult> ReviewReading(Guid id, RequestEndpoints.ReviewInput input, Access a, CubeDb db, CancellationToken ct)
    {
        Guard.That(input.Status is "Accepted" or "Rejected", "invalid_status"); var reason = Guard.Text(input.Reason, "reason", 2000);
        var reading = await Readings(a, db).AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new ApiError(404, "reading_not_found");
        // Compare-and-set prevents two reviewers from overwriting an already completed review.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await Readings(a, db).Where(r => r.Id == id && r.Status == "Submitted").ExecuteUpdateAsync(p => p.SetProperty(r => r.Status, input.Status).SetProperty(r => r.ReviewReason, reason), ct);
        Guard.That(changed == 1, "already_reviewed", 409);
        Events.Notify(db, reading.TenantId, "ReadingReviewed", id, reason); db.Audit.Add(new Audit { ActorId = a.UserId, Action = "ReviewReading", ResourceId = id });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { reading.Id, Status = input.Status, ReviewReason = reason });
    }
}

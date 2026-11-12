using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class RequestEndpoints
{
    public record Create(Guid CategoryId, Guid? PremisesId, string Subject, string Description, string ContactName, string Phone, string Email) : IApiRequest;
    public record MessageInput(string Text, bool Internal = false) : IApiRequest;
    public record Transition(string Status, string Reason, uint Version) : IApiRequest;
    public record Assignment(Guid? AssigneeId, DateTimeOffset? DueAt, uint Version) : IApiRequest;
    public record ReadingInput(decimal Value, Guid? PhotoId) : IApiRequest;
    public record ReviewInput(string Status, string Reason) : IApiRequest;
    public static void MapRequests(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Requests and readings");
        g.MapGet("/request-categories", async (CubeDb db, CancellationToken ct) => await db.Categories.OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Name, x.Department }).ToListAsync(ct));
        g.MapGet("/requests", async (Access a, string? status, Guid? premisesId, Guid? tenantId, Guid? assigneeId, int? page, int? size, CubeDb db, CancellationToken ct) =>
        {
            var q = a.Requests.AsNoTracking();
            if (status is not null) q = q.Where(x => x.Status == status); if (premisesId is not null) q = q.Where(x => x.PremisesId == premisesId);
            if (tenantId is not null) q = q.Where(x => x.TenantId == tenantId); if (assigneeId is not null) q = q.Where(x => x.AssigneeId == assigneeId);
            return Results.Ok(new { Total = await q.CountAsync(ct), Items = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(r => new { r.Id, r.TenantId, r.PremisesId, r.CategoryId, r.AssigneeId, r.Subject, r.Description, r.ContactName, r.Phone, r.Email, r.Status, r.DueAt, r.Version, r.CreatedAt, TenantName = db.Tenants.Where(t => t.Id == r.TenantId).Select(t => t.Name).FirstOrDefault(), PremisesName = db.Premises.Where(p => p.Id == r.PremisesId).Select(p => p.Name).FirstOrDefault(), AssigneeName = db.Users.Where(u => u.Id == r.AssigneeId).Select(u => u.FullName).FirstOrDefault() }).ToListAsync(ct) });
        });
        g.MapPost("/requests", async (Create input, Access a, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(!a.Staff, "tenant_required", 403); var tenant = a.TenantId;
            var category = await db.Categories.FindAsync([input.CategoryId], ct) ?? throw new ApiError(400, "invalid_category");
            if (input.PremisesId is Guid p) await a.ActivePremises(tenant, p, ct);
            var request = NewRequest(input, tenant, category); db.Requests.Add(request); Events.Queue(db, "Bitrix", "RequestCreated", request.Id); Events.NotifyStaff(db, request.AssigneeId, request.TenantId, "StaffRequestAssigned", request.Id, "Вам назначено обращение: " + request.Subject);
            await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/requests/{request.Id}", request);
        });
        g.MapGet("/requests/{id:guid}", async (Guid id, Access a, CubeDb db, CancellationToken ct) =>
        {
            var r = await a.Request(id, ct);
            return Results.Ok(new { Request = r, Messages = await db.Messages.Where(x => x.RequestId == id && x.TenantId == r.TenantId && (a.Staff || !x.Internal)).OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.AuthorId, AuthorName = db.Users.Where(u => u.Id == x.AuthorId).Select(u => u.FullName).FirstOrDefault(), x.Text, x.Internal, x.CreatedAt }).ToListAsync(ct), History = await db.RequestEvents.Where(x => x.RequestId == id && x.TenantId == r.TenantId).OrderBy(x => x.CreatedAt).ToListAsync(ct), Attachments = await db.Attachments.Where(x => x.RequestId == id && x.TenantId == r.TenantId && (a.Staff || x.MessageId == null || db.Messages.Any(m => m.Id == x.MessageId && m.TenantId == r.TenantId && !m.Internal))).Select(x => new { x.Id, x.FileId, x.MessageId, Name = db.Files.Where(f => f.Id == x.FileId).Select(f => f.Name).FirstOrDefault(), State = db.Files.Where(f => f.Id == x.FileId).Select(f => f.State).FirstOrDefault() }).ToListAsync(ct) });
        });
        g.MapPost("/requests/{id:guid}/messages", async (Guid id, MessageInput input, Access a, CubeDb db, CancellationToken ct) =>
        {
            var r = await a.Request(id, ct); Guard.That(!input.Internal || a.Staff, "internal_comment_forbidden", 403);
            Guard.That(r.Status is not "Closed" and not "Rejected", "request_closed", 409);
            var msg = new RequestMessage { RequestId = id, TenantId = r.TenantId, AuthorId = a.UserId, Text = Guard.Text(input.Text, "message", 10000), Internal = input.Internal };
            db.Messages.Add(msg); if (r.AssigneeId != a.UserId) Events.NotifyStaff(db, r.AssigneeId, r.TenantId, input.Internal ? "StaffRequestInternalComment" : "StaffRequestMessage", r.Id, input.Internal ? "Новая внутренняя заметка: " + r.Subject : "Новое сообщение: " + r.Subject); if (!input.Internal) { Events.Queue(db, "Bitrix", "MessageCreated", msg.Id); if (a.Staff) Events.Notify(db, r.TenantId, "RequestMessage", id, "New response on your request"); }
            await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/requests/{id}", new { msg.Id, msg.CreatedAt });
        });
        g.MapPut("/requests/{id:guid}/status", async (Guid id, Transition input, Access a, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(a.Staff, "staff_required", 403); var r = await a.Request(id, ct); Guard.That(r.Version == input.Version, "stale_version", 409);
            Guard.That(Rules.CanTransition(r.Status, input.Status), "invalid_transition", 409);
            db.RequestEvents.Add(new RequestEvent { TenantId = r.TenantId, RequestId = id, ActorId = a.UserId, FromStatus = r.Status, ToStatus = input.Status, Reason = Guard.Text(input.Reason, "reason", 2000) }); r.Status = input.Status;
            if (r.AssigneeId != a.UserId) Events.NotifyStaff(db, r.AssigneeId, r.TenantId, "StaffRequestStatus", r.Id, "Изменён статус обращения: " + r.Subject);
            var eventId = db.ChangeTracker.Entries<RequestEvent>().Single(x => x.State == EntityState.Added).Entity.Id;
            Events.Queue(db, "Bitrix", "RequestStatusChanged", eventId); Events.Notify(db, r.TenantId, "RequestStatus", id, "Your request status changed"); await db.SaveChangesAsync(ct); return Results.Ok(r);
        });
        g.MapPut("/admin/requests/{id:guid}/assignment", async (Guid id, Assignment input, CubeDb db, CancellationToken ct) =>
        {
            var r = await db.Requests.FindAsync([id], ct) ?? throw new ApiError(404, "request_not_found"); Guard.That(r.Version == input.Version, "stale_version", 409);
            if (input.AssigneeId is Guid uid) Guard.That(await db.Users.AnyAsync(x => x.Id == uid && (x.Role == "Staff" || x.Role == "Admin") && !x.Blocked && x.Approved, ct), "invalid_assignee");
            var reassigned = r.AssigneeId != input.AssigneeId; var deadlineChanged = r.DueAt != input.DueAt;
            r.AssigneeId = input.AssigneeId; r.DueAt = input.DueAt;
            if (reassigned || deadlineChanged) Events.NotifyStaff(db, r.AssigneeId, r.TenantId, reassigned ? "StaffRequestAssigned" : "StaffRequestDeadline", r.Id, (reassigned ? "Вам назначено обращение: " : "Изменён срок обращения: ") + r.Subject); await db.SaveChangesAsync(ct); return Results.Ok(r);
        }).RequireAuthorization("Admin");
        g.MapGet("/meters", async (Access a, CubeDb db, CancellationToken ct) => await a.Own<Meter>().AsNoTracking().OrderBy(x => x.SerialNumber).ToListAsync(ct));
        g.MapGet("/meters/{id:guid}/readings", async (Guid id, Access a, int? page, int? size, CancellationToken ct) =>
        {
            Guard.That(await a.Own<Meter>().AnyAsync(x => x.Id == id, ct), "meter_not_found", 404);
            return Results.Ok(await a.Own<Reading>().Where(x => x.MeterId == id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        });
        g.MapPost("/meters/{id:guid}/readings", async (Guid id, ReadingInput input, Access a, CubeDb db, CancellationToken ct) =>
        {
            var m = await a.Own<Meter>().SingleOrDefaultAsync(x => x.Id == id && x.Active, ct) ?? throw new ApiError(404, "meter_not_found"); await a.ActivePremises(a.TenantId, m.PremisesId, ct);
            Guard.That(input.Value >= m.LastValue && input.Value <= 999999999999m, "invalid_reading");
            if (input.PhotoId is Guid fid) Guard.That(await db.Files.AnyAsync(x => x.Id == fid && x.TenantId == a.TenantId && x.State == "Ready" && (x.ContentType == "image/jpeg" || x.ContentType == "image/png"), ct), "photo_not_available");
            var r = new Reading { TenantId = a.TenantId, MeterId = id, PreviousValue = m.LastValue, Value = input.Value, PhotoId = input.PhotoId };
            m.LastValue = input.Value; db.Readings.Add(r); Events.NotifyReadingStaff(db, m, r); Events.Queue(db, "Bitrix", "ReadingSubmitted", r.Id); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/meters/{id}/readings", r);
        });
        g.MapGet("/staff/meters", async (Access a, CubeDb db, int? page, int? size, CancellationToken ct) =>
        {
            Guard.That(a.Staff, "staff_required", 403);
            return Results.Ok(await StaffEndpoints.Meters(a, db).AsNoTracking().OrderBy(m => m.SerialNumber).ThenBy(m => m.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(m => new { m.Id, m.TenantId, m.PremisesId, m.Type, m.SerialNumber, m.Unit, m.LastValue, m.Active, m.Version, m.CreatedAt, TenantName = db.Tenants.Where(t => t.Id == m.TenantId).Select(t => t.Name).FirstOrDefault(), PremisesName = db.Premises.Where(p => p.Id == m.PremisesId).Select(p => p.Name).FirstOrDefault() }).ToListAsync(ct));
        });
        g.MapGet("/staff/readings", async (Access a, CubeDb db, int? page, int? size, CancellationToken ct) =>
        {
            Guard.That(a.Staff, "staff_required", 403);
            return Results.Ok(await StaffEndpoints.Readings(a, db).AsNoTracking().OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        });
        g.MapGet("/admin/readings", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Readings.Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPut("/admin/readings/{id:guid}/review", StaffEndpoints.ReviewReading).RequireAuthorization("Admin");
    }
    public static ServiceRequest NewRequest(Create i, Guid tenant, Category c) => new() { TenantId = tenant, CategoryId = c.Id, AssigneeId = c.ResponsibleUserId, PremisesId = i.PremisesId, Subject = Guard.Text(i.Subject, "subject"), Description = Guard.Text(i.Description, "description", 10000), ContactName = Guard.Text(i.ContactName, "contact"), Phone = Guard.Text(i.Phone, "phone", 40), Email = Guard.Email(i.Email) };
}

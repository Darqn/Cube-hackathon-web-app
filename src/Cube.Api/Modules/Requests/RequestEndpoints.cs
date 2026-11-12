using Cube.Api.Modules.Requests.Features;
using Cube.Api.Modules.Requests.Domain;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class RequestEndpoints
{
    public record Create(Guid CategoryId, Guid? PremisesId, string Subject, string Description, string ContactName, string Phone, string Email) : IApiRequest;
    public record MessageInput(string Text, bool Internal = false) : IApiRequest;
    public record Transition(string Status, string Reason, uint Version) : IApiRequest;
    public record Assignment(Guid? AssigneeId, DateTimeOffset? DueAt, uint Version) : IApiRequest;
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
        g.MapPost("/requests", async (Create input, CreateRequestHandler handler, HttpContext context, CancellationToken ct) =>
        {
            var result = await handler.Handle(input.CategoryId, new RequestContent(input.PremisesId, input.Subject, input.Description, input.ContactName, input.Phone, input.Email), ct);
            return result.ToHttpResult(context, r => Results.Created($"/api/v1/requests/{r.Id}", r));
        });
        g.MapGet("/requests/{id:guid}", async (Guid id, Access a, CubeDb db, CancellationToken ct) =>
        {
            var r = await a.Request(id, ct);
            return Results.Ok(new { Request = r, Messages = await db.Messages.Where(x => x.RequestId == id && x.TenantId == r.TenantId && (a.Staff || !x.Internal)).OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.AuthorId, AuthorName = db.Users.Where(u => u.Id == x.AuthorId).Select(u => u.FullName).FirstOrDefault(), x.Text, x.Internal, x.CreatedAt }).ToListAsync(ct), History = await db.RequestEvents.Where(x => x.RequestId == id && x.TenantId == r.TenantId).OrderBy(x => x.CreatedAt).ToListAsync(ct), Attachments = await db.Attachments.Where(x => x.RequestId == id && x.TenantId == r.TenantId && (a.Staff || x.MessageId == null || db.Messages.Any(m => m.Id == x.MessageId && m.TenantId == r.TenantId && !m.Internal))).Select(x => new { x.Id, x.FileId, x.MessageId, Name = db.Files.Where(f => f.Id == x.FileId).Select(f => f.Name).FirstOrDefault(), State = db.Files.Where(f => f.Id == x.FileId).Select(f => f.State).FirstOrDefault() }).ToListAsync(ct) });
        });
        g.MapPost("/requests/{id:guid}/messages", async (Guid id, MessageInput input, AddRequestMessageHandler handler, HttpContext context, CancellationToken ct) =>
        {
            var result = await handler.Handle(id, input.Text, input.Internal, ct);
            return result.ToHttpResult(context, msg => Results.Created($"/api/v1/requests/{id}", new { msg.Id, msg.CreatedAt }));
        });
        g.MapPut("/requests/{id:guid}/status", async (Guid id, Transition input, ChangeRequestStatusHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.Handle(id, input.Status, input.Reason, input.Version, ct)).ToHttpResult(context, Results.Ok));
        g.MapPut("/admin/requests/{id:guid}/assignment", async (Guid id, Assignment input, AssignRequestHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.Handle(id, input.AssigneeId, input.DueAt, input.Version, ct)).ToHttpResult(context, Results.Ok)).RequireAuthorization("Admin");

    }
}

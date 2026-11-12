using Cube.Api.Api.Files;
using Cube.Api.Infrastructure.Storage;
using Cube.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class FileEndpoints
{
    public record DocumentInput(Guid TenantId, Guid FileId, string Title, string Kind, string Number, DateOnly DocumentDate, Guid? LeaseId, Guid? PremisesId) : IApiRequest;
    public record ArchiveInput(bool Archived) : IApiRequest;
    public record StatementInput(Guid FileId, string Title, Guid? LeaseId, Guid? PremisesId) : IApiRequest;
    public static void MapFiles(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Files and documents");
        g.MapPost("/files", async (HttpRequest request, Guid? tenantId, Access a, CubeDb db, PrivateStorage storage, FileWriteBatch files, CancellationToken ct) =>
        {
            var owner = a.Admin ? tenantId ?? throw new ApiError(400, "tenant_id_required") : a.TenantId; Guard.That(!a.Staff || a.Admin, "staff_upload_forbidden", 403);
            Guard.That(await db.Tenants.AnyAsync(x => x.Id == owner, ct), "tenant_not_found", 404); Guard.That(request.HasFormContentType, "multipart_required");
            var form = await request.ReadFormAsync(ct);
            Guard.That(form.Files.Count == 1, "one_file_required");
            var upload = await UploadedFileReader.ReadAsync(form.Files[0], ct);
            var file = new StoredFile { TenantId = owner, UploadedBy = a.UserId, Key = Guid.NewGuid().ToString("N"), Name = upload.Name, ContentType = upload.ContentType, Size = upload.Bytes.LongLength, State = storage.AllowDevelopmentFiles ? "Ready" : "Quarantined" };
            db.Files.Add(file);
            await files.SaveAsync([new(file.Key, upload.Bytes)], async token => { await db.SaveChangesAsync(token); }, ct);
            return Results.Created($"/api/v1/files/{file.Id}", new { file.Id, file.Name, file.ContentType, file.Size, file.State });
        }).RequireRateLimiting("auth");
        g.MapGet("/files/{id:guid}", Download);
        g.MapGet("/invoices/{id:guid}/file", async (Guid id, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct) =>
        {
            var invoice = await a.Own<Invoice>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "invoice_not_found"); Guard.That(invoice.FileId.HasValue, "invoice_file_not_found", 404); return await Download(invoice.FileId!.Value, a, db, storage, ct);
        });
        g.MapGet("/documents", async (Access a, string? kind, int? page, int? size, CancellationToken ct) => await a.Own<Document>().Where(x => !x.Archived && (kind == null || x.Kind == kind)).OrderByDescending(x => x.DocumentDate).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/statements", async (StatementInput i, Access a, CubeDb db, TimeProvider clock, CancellationToken ct) =>
        {
            Guard.That(!a.Staff, "tenant_required", 403); await ValidateReferences(a.TenantId, i.FileId, i.LeaseId, i.PremisesId, db, ct);
            var d = new Document { TenantId = a.TenantId, FileId = i.FileId, Title = Guard.Text(i.Title, "title"), Kind = "Statement", DocumentDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), LeaseId = i.LeaseId, PremisesId = i.PremisesId, Status = "UnderReview" }; db.Documents.Add(d); Events.Queue(db, "Bitrix", "StatementSubmitted", d.Id); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/documents/{d.Id}", d);
        });
        g.MapGet("/documents/{id:guid}", async (Guid id, Access a, CancellationToken ct) => await a.Own<Document>().SingleOrDefaultAsync(x => x.Id == id && !x.Archived, ct) is Document d ? Results.Ok(d) : Results.NotFound());
        g.MapGet("/documents/{id:guid}/file", async (Guid id, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct) =>
        {
            var d = await a.Own<Document>().SingleOrDefaultAsync(x => x.Id == id && !x.Archived, ct) ?? throw new ApiError(404, "document_not_found"); return await Download(d.FileId, a, db, storage, ct);
        });
        g.MapGet("/admin/documents", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Documents.Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPost("/admin/documents", async (DocumentInput i, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(new[] { "Contract", "Addendum", "Statement", "Act", "Invoice", "UPD", "Other", "Template" }.Contains(i.Kind), "invalid_document_kind"); await ValidateReferences(i.TenantId, i.FileId, i.LeaseId, i.PremisesId, db, ct);
            var d = new Document { TenantId = i.TenantId, FileId = i.FileId, Title = Guard.Text(i.Title, "title"), Kind = i.Kind, Number = i.Number, DocumentDate = i.DocumentDate, LeaseId = i.LeaseId, PremisesId = i.PremisesId }; db.Documents.Add(d); await Events.NotifyAsync(db, d.TenantId, "DocumentAdded", d.Id, "A new document is available", ct); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/documents/{d.Id}", d);
        }).RequireAuthorization("Admin");
        g.MapPut("/admin/documents/{id:guid}/archive", async (Guid id, ArchiveInput i, CubeDb db, CancellationToken ct) => { var d = await db.Documents.FindAsync([id], ct) ?? throw new ApiError(404, "document_not_found"); d.Archived = i.Archived; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization("Admin");
        g.MapPut("/admin/statements/{id:guid}/review", async (Guid id, ReviewInput i, CubeDb db, CancellationToken ct) => { Guard.That(i.Status is "Accepted" or "Rejected", "invalid_status"); var d = await db.Documents.SingleOrDefaultAsync(x => x.Id == id && x.Kind == "Statement", ct) ?? throw new ApiError(404, "statement_not_found"); d.Status = i.Status; Guard.Text(i.Reason, "reason", 2000); await Events.NotifyAsync(db, d.TenantId, "StatementReviewed", d.Id, i.Reason, ct); await db.SaveChangesAsync(ct); return Results.Ok(d); }).RequireAuthorization("Admin");
    }
    private static async Task ValidateReferences(Guid tenant, Guid file, Guid? lease, Guid? premises, CubeDb db, CancellationToken ct)
    {
        Guard.That(await db.Files.AnyAsync(x => x.Id == file && x.TenantId == tenant && x.State == "Ready", ct), "file_not_found", 404);
        if (lease is Guid lid) Guard.That(await db.Leases.AnyAsync(x => x.Id == lid && x.TenantId == tenant && (premises == null || x.PremisesId == premises), ct), "lease_not_found", 404);
        if (premises is Guid p) Guard.That(await db.Leases.AnyAsync(x => x.TenantId == tenant && x.PremisesId == p, ct), "premises_not_found", 404);
    }
    private static async Task<IResult> Download(Guid id, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct)
    {
        var f = await db.Files.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.State == "Ready", ct) ?? throw new ApiError(404, "file_not_found");
        if (!a.Admin)
        {
            if (a.Staff) Guard.That(await (from attachment in db.Attachments join r in a.Requests on attachment.RequestId equals r.Id where attachment.FileId == id && attachment.TenantId == f.TenantId select attachment.Id).AnyAsync(ct), "file_not_found", 404);
            else { Guard.That(f.TenantId == a.TenantId, "file_not_found", 404); Guard.That(!await db.Attachments.AnyAsync(at => at.FileId == id && at.MessageId != null && db.Messages.Any(m => m.Id == at.MessageId && m.Internal), ct), "file_not_found", 404); }
        }
        var path = storage.PathFor(f.Key); Guard.That(File.Exists(path), "file_not_found", 404); return Results.File(path, f.ContentType, f.Name, enableRangeProcessing: true);
    }
}

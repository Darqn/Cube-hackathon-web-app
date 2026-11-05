using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public sealed class PrivateStorage(IConfiguration config, IWebHostEnvironment environment)
{
    public string Root { get; } = Path.GetFullPath(config["Files:Root"] ?? Path.Combine(environment.ContentRootPath, ".data", "files"));
    public bool AllowDevelopmentFiles => environment.IsDevelopment() && config.GetValue<bool>("Files:AllowUnscannedDevelopmentUploads");
    public string PathFor(string key) { Guard.That(key.Length == 32 && key.All(Uri.IsHexDigit), "invalid_storage_key"); return Path.Combine(Root, key); }
    public static string Validate(byte[] bytes, string filename)
    {
        Guard.That(bytes.Length > 0 && bytes.Length <= 20 * 1024 * 1024, "invalid_file_size", 413);
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        bool Is(params byte[] prefix) => bytes.AsSpan().StartsWith(prefix);
        return ext switch
        {
            ".pdf" when Is(37, 80, 68, 70, 45) => "application/pdf",
            ".jpg" or ".jpeg" when Is(255, 216, 255) => "image/jpeg",
            ".png" when Is(137, 80, 78, 71, 13, 10, 26, 10) => "image/png",
            ".doc" when Is(208, 207, 17, 224, 161, 177, 26, 225) => "application/msword",
            ".xls" when Is(208, 207, 17, 224, 161, 177, 26, 225) => "application/vnd.ms-excel",
            ".docx" or ".xlsx" when Is(80, 75, 3, 4) => ValidateOffice(bytes, ext),
            _ => throw new ApiError(400, "unsupported_or_invalid_file")
        };
    }
    private static string ValidateOffice(byte[] bytes, string extension)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            Guard.That(zip.Entries.Count <= 2000 && zip.Entries.Sum(x => x.Length) <= 100 * 1024 * 1024, "unsafe_office_archive");
            Guard.That(zip.Entries.All(x => !x.FullName.Contains("..") && !x.FullName.StartsWith('/') && !x.FullName.Contains('\\') && !x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)), "unsafe_office_archive");
            Guard.That(zip.GetEntry("[Content_Types].xml") is not null && zip.GetEntry(extension == ".docx" ? "word/document.xml" : "xl/workbook.xml") is not null, "invalid_office_document");
            return extension == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }
        catch (InvalidDataException) { throw new ApiError(400, "invalid_office_document"); }
    }
}
public static class FileEndpoints
{
    public record DocumentInput(Guid TenantId, Guid FileId, string Title, string Kind, string Number, DateOnly DocumentDate, Guid? LeaseId, Guid? PremisesId);
    public record ArchiveInput(bool Archived);
    public record StatementInput(Guid FileId, string Title, Guid? LeaseId, Guid? PremisesId);
    public record AttachmentInput(Guid FileId);
    public static void MapFiles(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Files and documents");
        g.MapPost("/files", async (HttpRequest request, Guid? tenantId, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct) =>
        {
            var owner = a.Admin ? tenantId ?? throw new ApiError(400, "tenant_id_required") : a.TenantId; Guard.That(!a.Staff || a.Admin, "staff_upload_forbidden", 403);
            Guard.That(await db.Tenants.AnyAsync(x => x.Id == owner, ct), "tenant_not_found", 404); Guard.That(request.HasFormContentType, "multipart_required");
            var form = await request.ReadFormAsync(ct); Guard.That(form.Files.Count == 1, "one_file_required"); var upload = form.Files[0]; Guard.That(upload.Length > 0 && upload.Length <= 20 * 1024 * 1024, "invalid_file_size", 413);
            var name = Path.GetFileName(upload.FileName); Guard.That(name.Length is > 0 and <= 200 && !name.Any(char.IsControl), "invalid_filename");
            using var buffer = new MemoryStream(); await upload.CopyToAsync(buffer, ct); var bytes = buffer.ToArray(); var contentType = PrivateStorage.Validate(bytes, name);
            var file = new StoredFile { TenantId = owner, UploadedBy = a.UserId, Key = Guid.NewGuid().ToString("N"), Name = name, ContentType = contentType, Size = bytes.LongLength, State = storage.AllowDevelopmentFiles ? "Ready" : "Quarantined" };
            Directory.CreateDirectory(storage.Root); var path = storage.PathFor(file.Key); await File.WriteAllBytesAsync(path, bytes, ct);
            try { db.Files.Add(file); await db.SaveChangesAsync(ct); } catch { File.Delete(path); throw; }
            return Results.Created($"/api/v1/files/{file.Id}", new { file.Id, file.Name, file.ContentType, file.Size, file.State });
        }).RequireRateLimiting("auth");
        g.MapGet("/files/{id:guid}", Download);
        g.MapGet("/invoices/{id:guid}/file", async (Guid id, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct) =>
        {
            var invoice = await a.Own<Invoice>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "invoice_not_found"); Guard.That(invoice.FileId.HasValue, "invoice_file_not_found", 404); return await Download(invoice.FileId!.Value, a, db, storage, ct);
        });
        g.MapGet("/documents", async (Access a, string? kind, int? page, int? size, CancellationToken ct) => await a.Own<Document>().Where(x => !x.Archived && (kind == null || x.Kind == kind)).OrderByDescending(x => x.DocumentDate).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/statements", async (StatementInput i, Access a, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(!a.Staff, "tenant_required", 403); await ValidateReferences(a.TenantId, i.FileId, i.LeaseId, i.PremisesId, db, ct);
            var d = new Document { TenantId = a.TenantId, FileId = i.FileId, Title = Guard.Text(i.Title, "title"), Kind = "Statement", DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow), LeaseId = i.LeaseId, PremisesId = i.PremisesId, Status = "UnderReview" }; db.Documents.Add(d); Events.Queue(db, "Bitrix", "StatementSubmitted", d.Id); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/documents/{d.Id}", d);
        });
        g.MapGet("/documents/{id:guid}", async (Guid id, Access a, CancellationToken ct) => await a.Own<Document>().SingleOrDefaultAsync(x => x.Id == id && !x.Archived, ct) is Document d ? Results.Ok(d) : Results.NotFound());
        g.MapGet("/documents/{id:guid}/file", async (Guid id, Access a, CubeDb db, PrivateStorage storage, CancellationToken ct) =>
        {
            var d = await a.Own<Document>().SingleOrDefaultAsync(x => x.Id == id && !x.Archived, ct) ?? throw new ApiError(404, "document_not_found"); return await Download(d.FileId, a, db, storage, ct);
        });
        g.MapPost("/requests/{id:guid}/attachments", async (Guid id, AttachmentInput i, Access a, CubeDb db, CancellationToken ct) =>
        {
            var r = await a.Request(id, ct); Guard.That(a.Admin || !a.Staff, "staff_upload_forbidden", 403); Guard.That(r.Status is not "Closed" and not "Rejected", "request_closed", 409);
            Guard.That(await db.Files.AnyAsync(x => x.Id == i.FileId && x.TenantId == r.TenantId && x.State == "Ready", ct), "file_not_found", 404);
            var at = new Attachment { TenantId = r.TenantId, RequestId = id, FileId = i.FileId }; db.Attachments.Add(at); Events.Queue(db, "Bitrix", "AttachmentAdded", at.Id); if (r.AssigneeId != a.UserId) Events.NotifyStaff(db, r.AssigneeId, r.TenantId, "StaffRequestAttachment", r.Id, "Добавлено вложение: " + r.Subject); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/requests/{id}", new { at.Id, at.FileId });
        });
        g.MapGet("/admin/documents", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Documents.Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPost("/admin/documents", async (DocumentInput i, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(new[] { "Contract", "Addendum", "Statement", "Act", "Invoice", "UPD", "Other", "Template" }.Contains(i.Kind), "invalid_document_kind"); await ValidateReferences(i.TenantId, i.FileId, i.LeaseId, i.PremisesId, db, ct);
            var d = new Document { TenantId = i.TenantId, FileId = i.FileId, Title = Guard.Text(i.Title, "title"), Kind = i.Kind, Number = i.Number, DocumentDate = i.DocumentDate, LeaseId = i.LeaseId, PremisesId = i.PremisesId }; db.Documents.Add(d); Events.Notify(db, d.TenantId, "DocumentAdded", d.Id, "A new document is available"); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/documents/{d.Id}", d);
        }).RequireAuthorization("Admin");
        g.MapPut("/admin/documents/{id:guid}/archive", async (Guid id, ArchiveInput i, CubeDb db, CancellationToken ct) => { var d = await db.Documents.FindAsync([id], ct) ?? throw new ApiError(404, "document_not_found"); d.Archived = i.Archived; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization("Admin");
        g.MapPut("/admin/statements/{id:guid}/review", async (Guid id, RequestEndpoints.ReviewInput i, CubeDb db, CancellationToken ct) => { Guard.That(i.Status is "Accepted" or "Rejected", "invalid_status"); var d = await db.Documents.SingleOrDefaultAsync(x => x.Id == id && x.Kind == "Statement", ct) ?? throw new ApiError(404, "statement_not_found"); d.Status = i.Status; Guard.Text(i.Reason, "reason", 2000); Events.Notify(db, d.TenantId, "StatementReviewed", d.Id, i.Reason); await db.SaveChangesAsync(ct); return Results.Ok(d); }).RequireAuthorization("Admin");
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

using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class TenantEndpoints
{
    public record ListingInput(Guid PremisesId, string Title, string Description, bool Published);
    public record InquiryInput(string ContactName, string Phone, string Email, string Message);
    public record InvoiceInput(Guid TenantId, string Number, string Kind, DateOnly IssuedOn, DateOnly DueOn, string Purpose, decimal Amount, Guid? FileId);
    public static void MapTenantFeatures(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Tenant portal");
        g.MapGet("/me/leases", async (Access a, CubeDb db, int? page, int? size, CancellationToken ct) => await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id orderby l.StartsOn descending, l.Id select new { Lease = l, Premises = p }).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/me/premises", async (Access a, CubeDb db, CancellationToken ct) => { var today = DateOnly.FromDateTime(DateTime.UtcNow); return await (from l in a.Own<Lease>() join p in db.Premises on l.PremisesId equals p.Id where l.Status == "Active" && l.StartsOn <= today && l.EndsOn >= today select new { Lease = l, Premises = p }).Take(100).ToListAsync(ct); });
        g.MapGet("/invoices", async (Access a, CubeDb db, string? kind, int? page, int? size, TimeProvider clock, CancellationToken ct) =>
        {
            var q = a.Own<Invoice>().AsNoTracking().Where(x => kind == null || x.Kind == kind); var items = await q.OrderByDescending(x => x.IssuedOn).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct);
            return Results.Ok(new { Total = await q.CountAsync(ct), Items = items.Select(i => InvoiceDto(i, clock)) });
        });
        g.MapGet("/invoices/{id:guid}", async (Guid id, Access a, TimeProvider clock, CancellationToken ct) => { var i = await a.Own<Invoice>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "invoice_not_found"); return Results.Ok(InvoiceDto(i, clock)); });
        g.MapPost("/invoices/{id:guid}/payment-attempts", async (Guid id, Access a, IPaymentGateway gateway, HttpRequest request, CancellationToken ct) =>
        {
            var i = await a.Own<Invoice>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "invoice_not_found"); Guard.That(i.Paid < i.Amount, "invoice_already_paid", 409);
            var key = Guard.Text(request.Headers["Idempotency-Key"], "idempotency_key", 100); var url = await gateway.CreateCheckoutAsync(i, key, ct); return Results.Ok(new { CheckoutUrl = url });
        });
        g.MapGet("/payments", async (Access a, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Set<PaymentRecord>().Where(x => x.TenantId == a.TenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(x => new { x.Id, x.InvoiceId, x.Amount, x.Currency, x.CreatedAt, Status = "Succeeded" }).ToListAsync(ct));
        g.MapPost("/admin/invoices", async (InvoiceInput i, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(await db.Tenants.AnyAsync(x => x.Id == i.TenantId, ct), "tenant_not_found", 404); Guard.That(i.Amount > 0 && decimal.Round(i.Amount, 2) == i.Amount && i.DueOn >= i.IssuedOn, "invalid_invoice"); Guard.That(i.Kind is "Rent" or "Utilities" or "VehicleAccess" or "Other", "invalid_kind");
            if (i.FileId is Guid f) Guard.That(await db.Files.AnyAsync(x => x.Id == f && x.TenantId == i.TenantId && x.State == "Ready" && x.ContentType == "application/pdf", ct), "invalid_invoice_file");
            var v = new Invoice { TenantId = i.TenantId, ExternalId = $"manual:{Guid.NewGuid()}", Number = Guard.Text(i.Number, "number"), Kind = i.Kind, IssuedOn = i.IssuedOn, DueOn = i.DueOn, Purpose = Guard.Text(i.Purpose, "purpose", 1000), Amount = i.Amount, FileId = i.FileId }; db.Invoices.Add(v); Events.Notify(db, i.TenantId, "InvoiceCreated", v.Id, "A new invoice is available"); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/invoices/{v.Id}", v);
        }).RequireAuthorization("Admin");
        g.MapGet("/admin/invoices", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Invoices.Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.IssuedOn).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapGet("/notifications", async (Access a, bool? unreadOnly, int? page, int? size, CancellationToken ct) => await a.Own<Notification>().Where(x => (x.UserId == null || x.UserId == a.UserId) && (unreadOnly != true || !x.Read)).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/notifications/{id:guid}/read", async (Guid id, Access a, CubeDb db, CancellationToken ct) => { var n = await a.Own<Notification>().SingleOrDefaultAsync(x => x.Id == id && (x.UserId == null || x.UserId == a.UserId), ct) ?? throw new ApiError(404, "notification_not_found"); n.Read = true; await db.SaveChangesAsync(ct); return Results.NoContent(); });
        g.MapGet("/dashboard", async (Access a, CubeDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime); var invoices = await a.Own<Invoice>().Where(x => x.Paid < x.Amount).ToListAsync(ct);
            var visibleNews = await NewsEndpoints.Visible(a, db, clock).OrderByDescending(x => x.PublishAt).Take(100).ToListAsync(ct);
            return Results.Ok(new { Organization = await db.Tenants.Where(x => x.Id == a.TenantId).Select(x => new { x.Name, x.Inn }).SingleAsync(ct), ActiveLeases = await a.Own<Lease>().CountAsync(x => x.Status == "Active" && x.StartsOn <= today && x.EndsOn >= today, ct), OutstandingAmount = invoices.Sum(x => x.Amount - x.Paid), OverdueAmount = invoices.Where(x => x.DueOn < today).Sum(x => x.Amount - x.Paid), NextPayment = invoices.Where(x => x.DueOn >= today).OrderBy(x => x.DueOn).Select(x => new { x.Id, x.DueOn, Balance = x.Amount - x.Paid }).FirstOrDefault(), UnreadNotifications = await a.Own<Notification>().CountAsync(x => !x.Read && (x.UserId == null || x.UserId == a.UserId), ct), ImportantNews = visibleNews.Where(x => x.Importance != "Normal").OrderByDescending(x => x.Importance == "Urgent").Take(10).Select(NewsEndpoints.Dto) });
        });
        g.MapGet("/listings", async (string? type, decimal? minArea, decimal? maxArea, decimal? minPrice, decimal? maxPrice, string? building, int? floor, int? page, int? size, CubeDb db, CancellationToken ct) =>
        {
            var q = from l in db.Listings join p in db.Premises on l.PremisesId equals p.Id where l.Published && !l.Archived && (p.Status == "Available" || p.Status == "Reserved") && (type == null || p.Type == type) && (minArea == null || p.Area >= minArea) && (maxArea == null || p.Area <= maxArea) && (minPrice == null || p.MonthlyRent >= minPrice) && (maxPrice == null || p.MonthlyRent <= maxPrice) && (building == null || p.Building == building) && (floor == null || p.Floor == floor) orderby l.CreatedAt descending, l.Id select new { Listing = l, Premises = p };
            return Results.Ok(new { Total = await q.CountAsync(ct), Items = await q.Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct) });
        });
        g.MapGet("/listings/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await (from l in db.Listings join p in db.Premises on l.PremisesId equals p.Id where l.Id == id && l.Published && !l.Archived && (p.Status == "Available" || p.Status == "Reserved") select new { Listing = l, Premises = p }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound());
        g.MapPost("/listings/{id:guid}/inquiries", async (Guid id, InquiryInput i, Access a, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(!a.Staff, "tenant_required", 403); var existing = await db.Inquiries.SingleOrDefaultAsync(x => x.TenantId == a.TenantId && x.ListingId == id, ct); if (existing is not null) return Results.Ok(existing);
            var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == id && x.Published && !x.Archived, ct) ?? throw new ApiError(404, "listing_not_found"); Guard.That(await db.Premises.AnyAsync(x => x.Id == listing.PremisesId && x.Status == "Available", ct), "listing_unavailable", 409);
            var category = await db.Categories.SingleAsync(x => x.Code == "rent", ct);
            var r = RequestEndpoints.NewRequest(new(category.Id, null, $"Rental inquiry: {listing.Title}", Guard.Text(i.Message, "message", 10000), i.ContactName, i.Phone, i.Email), a.TenantId, category);
            db.Requests.Add(r); var inquiry = new ListingInquiry { TenantId = a.TenantId, ListingId = id, RequestId = r.Id }; db.Inquiries.Add(inquiry); Events.Queue(db, "Bitrix", "RequestCreated", r.Id); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/requests/{r.Id}", inquiry);
        });
        g.MapGet("/me/listing-inquiries", async (Access a, int? page, int? size, CancellationToken ct) => await a.Own<ListingInquiry>().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/admin/listings", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Listings.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPost("/admin/listings", async (ListingInput i, CubeDb db, CancellationToken ct) => { Guard.That(await db.Premises.AnyAsync(x => x.Id == i.PremisesId, ct), "premises_not_found", 404); var l = new Listing { PremisesId = i.PremisesId, Title = Guard.Text(i.Title, "title"), Description = Guard.Text(i.Description, "description", 10000), Published = i.Published }; db.Listings.Add(l); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/listings/{l.Id}", l); }).RequireAuthorization("Admin");
        g.MapPut("/admin/listings/{id:guid}", async (Guid id, ListingInput i, CubeDb db, CancellationToken ct) => { var l = await db.Listings.FindAsync([id], ct) ?? throw new ApiError(404, "listing_not_found"); Guard.That(l.PremisesId == i.PremisesId, "cannot_change_listing_premises"); l.Title = Guard.Text(i.Title, "title"); l.Description = Guard.Text(i.Description, "description", 10000); l.Published = i.Published; await db.SaveChangesAsync(ct); return Results.Ok(l); }).RequireAuthorization("Admin");
        g.MapDelete("/admin/listings/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => { var l = await db.Listings.FindAsync([id], ct) ?? throw new ApiError(404, "listing_not_found"); l.Archived = true; l.Published = false; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization("Admin");
    }
    private static object InvoiceDto(Invoice i, TimeProvider clock) => new { i.Id, i.Number, i.Kind, i.IssuedOn, i.DueOn, i.Purpose, i.Amount, i.Paid, Balance = i.Amount - i.Paid, i.Currency, i.FileId, Status = Rules.InvoiceStatus(i, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), PaymentState = i.Paid >= i.Amount ? "Paid" : i.Paid > 0 ? "PartiallyPaid" : "Unpaid", CanPay = false };
}

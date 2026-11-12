using Microsoft.EntityFrameworkCore;
using static Cube.Api.TenantEndpoints;
using Cube.Api.Workflows;
using Cube.Api.Modules.Billing.Domain;

namespace Cube.Api;

public static class BillingTenantEndpoints
{
    public static void MapTenantBilling(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Tenant portal");
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
            var v = new Invoice { TenantId = i.TenantId, ExternalId = $"manual:{Guid.NewGuid()}", Number = Guard.Text(i.Number, "number"), Kind = i.Kind, IssuedOn = i.IssuedOn, DueOn = i.DueOn, Purpose = Guard.Text(i.Purpose, "purpose", 1000), Amount = i.Amount, FileId = i.FileId }; db.Invoices.Add(v); await Events.NotifyAsync(db, i.TenantId, "InvoiceCreated", v.Id, "A new invoice is available", ct); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/invoices/{v.Id}", v);
        }).RequireAuthorization("Admin");
        g.MapGet("/admin/invoices", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Invoices.Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.IssuedOn).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");

    }
    private static object InvoiceDto(Invoice i, TimeProvider clock) => new { i.Id, i.Number, i.Kind, i.IssuedOn, i.DueOn, i.Purpose, i.Amount, i.Paid, Balance = i.Amount - i.Paid, i.Currency, i.FileId, Status = InvoiceRules.InvoiceStatus(i, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), PaymentState = i.Paid >= i.Amount ? "Paid" : i.Paid > 0 ? "PartiallyPaid" : "Unpaid", CanPay = false };
}

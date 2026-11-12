using Microsoft.EntityFrameworkCore;
using static Cube.Api.AdminEndpoints;
using Microsoft.AspNetCore.Identity;
using Cube.Api.Modules.Identity.Domain;

namespace Cube.Api;

public static class PropertyAdminEndpoints
{
    public static void MapAdminProperty(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").AddEndpointFilter<InputValidationFilter>().RequireAuthorization("Admin").WithTags("Administration");
        g.MapGet("/tenants", async (CubeDb db, string? search, int? page, int? size, CancellationToken ct) => await db.Tenants.Where(x => search == null || x.Name.Contains(search) || x.Inn.Contains(search)).OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/tenants/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await db.Tenants.FindAsync([id], ct) is Tenant t ? Results.Ok(new { Tenant = t, Leases = await db.Leases.Where(x => x.TenantId == id).ToListAsync(ct), InvoiceCount = await db.Invoices.CountAsync(x => x.TenantId == id, ct), RequestCount = await db.Requests.CountAsync(x => x.TenantId == id, ct) }) : Results.NotFound());
        g.MapPost("/tenants", async (TenantInput i, CubeDb db, CancellationToken ct) => { var t = new Tenant(); SetTenant(t, i); db.Tenants.Add(t); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/tenants/{t.Id}", t); });
        g.MapPut("/tenants/{id:guid}", async (Guid id, TenantInput i, CubeDb db, CancellationToken ct) => { var t = await db.Tenants.FindAsync([id], ct) ?? throw new ApiError(404, "tenant_not_found"); SetTenant(t, i); await db.SaveChangesAsync(ct); return Results.Ok(t); });
        g.MapPut("/tenants/{id:guid}/blocked", async (Guid id, BlockInput i, CubeDb db, CancellationToken ct) => { var t = await db.Tenants.FindAsync([id], ct) ?? throw new ApiError(404, "tenant_not_found"); t.Blocked = i.Blocked; await db.SaveChangesAsync(ct); return Results.NoContent(); });
        g.MapGet("/premises", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Premises.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapPost("/premises", async (PremisesInput i, CubeDb db, CancellationToken ct) => { var p = new Premises(); SetPremises(p, i); db.Premises.Add(p); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/premises/{p.Id}", p); });
        g.MapPut("/premises/{id:guid}", async (Guid id, PremisesInput i, CubeDb db, CancellationToken ct) => { var p = await db.Premises.FindAsync([id], ct) ?? throw new ApiError(404, "premises_not_found"); SetPremises(p, i); if (i.Status == "Available") Guard.That(!await db.Leases.AnyAsync(x => x.PremisesId == id && x.Status == "Active", ct), "premises_has_active_lease", 409); await db.SaveChangesAsync(ct); return Results.Ok(p); });
        g.MapPost("/leases", async (LeaseInput i, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(i.StartsOn <= i.EndsOn && i.SignedOn <= i.EndsOn, "invalid_lease_dates"); Guard.That(await db.Tenants.AnyAsync(x => x.Id == i.TenantId, ct), "tenant_not_found", 404);
            var p = await db.Premises.FindAsync([i.PremisesId], ct) ?? throw new ApiError(404, "premises_not_found"); Guard.That(p.Status is "Available" or "Reserved", "premises_unavailable", 409);
            var l = new Lease { TenantId = i.TenantId, PremisesId = i.PremisesId, Number = Guard.Text(i.Number, "number"), SignedOn = i.SignedOn, StartsOn = i.StartsOn, EndsOn = i.EndsOn }; db.Leases.Add(l); p.Status = "Occupied"; await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/me/leases/{l.Id}", l);
        });
        g.MapPut("/leases/{id:guid}/status", async (Guid id, LeaseStatus i, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(i.Status is "Expired" or "Terminated", "invalid_status"); var l = await db.Leases.FindAsync([id], ct) ?? throw new ApiError(404, "lease_not_found"); Guard.That(l.Status == "Active", "inactive_lease", 409); l.Status = i.Status; var p = await db.Premises.SingleAsync(x => x.Id == l.PremisesId, ct); p.Status = "Available"; await db.SaveChangesAsync(ct); return Results.Ok(l);
        });
        g.MapGet("/leases", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Leases.AsNoTracking().Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));

    }
    private static void SetTenant(Tenant t, TenantInput i) { Guard.That(InnRules.ValidInn(i.Inn), "invalid_inn"); t.Name = Guard.Text(i.Name, "name"); t.Inn = i.Inn; t.ContactName = Guard.Text(i.ContactName, "contact"); t.Email = Guard.Email(i.Email); t.Phone = Guard.Text(i.Phone, "phone", 40); }
    private static void SetPremises(Premises p, PremisesInput i)
    {
        Guard.That(i.Type is "Office" or "Warehouse" or "Retail", "invalid_type"); Guard.That(i.Status is "Available" or "Occupied" or "Reserved" or "Unavailable", "invalid_status"); Guard.That(i.Area > 0 && i.MonthlyRent >= 0, "invalid_dimensions");
        p.Name = Guard.Text(i.Name, "name"); p.Type = i.Type; p.Address = Guard.Text(i.Address, "address", 500); p.Building = Guard.Text(i.Building, "building"); p.Section = i.Section; p.Floor = i.Floor; p.Area = i.Area; p.MonthlyRent = i.MonthlyRent; p.Description = Guard.Text(i.Description, "description", 10000); p.Utilities = i.Utilities; p.Status = i.Status;
    }
}

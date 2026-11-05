using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class AdminEndpoints
{
    public record TenantInput(string Name, string Inn, string ContactName, string Email, string Phone);
    public record Approval(Guid? ExistingTenantId);
    public record BlockInput(bool Blocked);
    public record StaffInput(string Login, string Email, string Password, string FullName);
    public record PremisesInput(string Name, string Type, string Address, string Building, string Section, int? Floor, decimal Area, decimal MonthlyRent, string Description, string Utilities, string Status);
    public record LeaseInput(Guid TenantId, Guid PremisesId, string Number, DateOnly SignedOn, DateOnly StartsOn, DateOnly EndsOn);
    public record LeaseStatus(string Status);
    public record MeterInput(Guid TenantId, Guid PremisesId, string Type, string SerialNumber, string Unit, decimal Baseline);
    public record MeterBaseline(decimal Value, string Reason, uint Version);
    public record Routing(Guid? ResponsibleUserId);
    public static void MapAdmin(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").AddEndpointFilter<InputValidationFilter>().RequireAuthorization("Admin").WithTags("Administration");
        g.MapGet("/registration-applications", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Users.Where(x => !x.Approved && x.Role == "Tenant").OrderBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(x => new { x.Id, x.UserName, x.Email, x.FullName, x.PhoneNumber, x.RequestedOrganization, x.RequestedInn, x.Blocked }).ToListAsync(ct));
        g.MapPost("/registration-applications/{id:guid}/approve", async (Guid id, Approval input, UserManager<User> users, CubeDb db, Access a, CancellationToken ct) =>
        {
            var u = await users.FindByIdAsync(id.ToString()) ?? throw new ApiError(404, "application_not_found"); Guard.That(!u.Approved && u.Role == "Tenant", "already_processed", 409);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            Tenant t;
            if (input.ExistingTenantId is Guid tid) { t = await db.Tenants.FindAsync([tid], ct) ?? throw new ApiError(404, "tenant_not_found"); Guard.That(t.Inn == u.RequestedInn, "organization_inn_mismatch"); }
            else { Guard.That(!await db.Tenants.AnyAsync(x => x.Inn == u.RequestedInn, ct), "existing_tenant_requires_explicit_association", 409); t = new Tenant { Name = u.RequestedOrganization, Inn = u.RequestedInn, ContactName = u.FullName, Email = u.Email!, Phone = u.PhoneNumber! }; db.Tenants.Add(t); await db.SaveChangesAsync(ct); }
            u.TenantId = t.Id; u.Approved = true; u.Blocked = false; Guard.That((await users.UpdateAsync(u)).Succeeded, "approval_conflict", 409);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = "ApproveRegistration", ResourceId = id }); Events.Queue(db, "Email", "AccountApproved", id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { u.Id, u.TenantId, Status = "Approved" });
        });
        g.MapPut("/users/{id:guid}/blocked", async (Guid id, BlockInput input, UserManager<User> users, Access a, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(id != a.UserId, "cannot_block_self"); var u = await users.FindByIdAsync(id.ToString()) ?? throw new ApiError(404, "user_not_found"); u.Blocked = input.Blocked; await users.UpdateSecurityStampAsync(u); Guard.That((await users.UpdateAsync(u)).Succeeded, "user_update_failed", 409);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = input.Blocked ? "BlockUser" : "UnblockUser", ResourceId = id }); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        g.MapPost("/staff", async (StaffInput input, UserManager<User> users, Access a, CubeDb db, CancellationToken ct) =>
        {
            var u = new User { Id = Guid.NewGuid(), UserName = Guard.Text(input.Login, "login", 100), Email = Guard.Email(input.Email), FullName = Guard.Text(input.FullName, "name"), Role = "Staff", Approved = true };
            Guard.That((await users.CreateAsync(u, input.Password)).Succeeded, "staff_creation_failed"); db.Audit.Add(new Audit { ActorId = a.UserId, Action = "CreateStaff", ResourceId = u.Id }); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/users/{u.Id}", new { u.Id, u.UserName, u.Role });
        });
        g.MapGet("/users", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Users.OrderBy(x => x.UserName).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(x => new { x.Id, x.UserName, x.FullName, x.Role, x.TenantId, x.Approved, x.Blocked }).ToListAsync(ct));
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
        g.MapPost("/meters", async (MeterInput i, CubeDb db, Access a, CancellationToken ct) =>
        {
            Guard.That(i.Type is "Water" or "Electricity" && i.Baseline >= 0, "invalid_meter"); await a.ActivePremises(i.TenantId, i.PremisesId, ct);
            var m = new Meter { TenantId = i.TenantId, PremisesId = i.PremisesId, Type = i.Type, SerialNumber = Guard.Text(i.SerialNumber, "serial"), Unit = Guard.Text(i.Unit, "unit", 30), LastValue = i.Baseline }; db.Meters.Add(m); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/meters/{m.Id}", m);
        });
        g.MapPut("/meters/{id:guid}/baseline", async (Guid id, MeterBaseline i, CubeDb db, CancellationToken ct) =>
        {
            var m = await db.Meters.FindAsync([id], ct) ?? throw new ApiError(404, "meter_not_found"); Guard.That(i.Value >= 0, "invalid_baseline"); Guard.That(i.Version == m.Version, "stale_version", 409); Guard.Text(i.Reason, "reason", 2000);
            // Corrections are separate history records; never rewrite a submitted reading.
            db.Readings.Add(new Reading { TenantId = m.TenantId, MeterId = id, PreviousValue = i.Value, Value = i.Value, Status = "Correction", ReviewReason = i.Reason }); m.LastValue = i.Value; await db.SaveChangesAsync(ct); return Results.Ok(m);
        });
        g.MapPut("/request-categories/{id:guid}/routing", async (Guid id, Routing i, CubeDb db, CancellationToken ct) =>
        {
            var c = await db.Categories.FindAsync([id], ct) ?? throw new ApiError(404, "category_not_found"); if (i.ResponsibleUserId is Guid uid) Guard.That(await db.Users.AnyAsync(x => x.Id == uid && (x.Role == "Staff" || x.Role == "Admin") && x.Approved && !x.Blocked, ct), "invalid_assignee"); c.ResponsibleUserId = i.ResponsibleUserId; await db.SaveChangesAsync(ct); return Results.Ok(c);
        });
        g.MapGet("/leases", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Leases.AsNoTracking().Where(x => tenantId == null || x.TenantId == tenantId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/meters", async (Guid? tenantId, CubeDb db, int? page, int? size, CancellationToken ct) => await db.Meters.AsNoTracking().Where(x => tenantId == null || x.TenantId == tenantId).OrderBy(x => x.SerialNumber).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/audit", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Audit.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/integrations", () => new { Accounting = "NotConfigured", Bitrix = "NotConfigured", Payments = "NotConfigured", Email = "NotConfigured", AutomaticDelivery = false });
        g.MapGet("/integration-handoffs", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Outbox.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
    }
    private static void SetTenant(Tenant t, TenantInput i) { Guard.That(Rules.ValidInn(i.Inn), "invalid_inn"); t.Name = Guard.Text(i.Name, "name"); t.Inn = i.Inn; t.ContactName = Guard.Text(i.ContactName, "contact"); t.Email = Guard.Email(i.Email); t.Phone = Guard.Text(i.Phone, "phone", 40); }
    private static void SetPremises(Premises p, PremisesInput i)
    {
        Guard.That(i.Type is "Office" or "Warehouse" or "Retail", "invalid_type"); Guard.That(i.Status is "Available" or "Occupied" or "Reserved" or "Unavailable", "invalid_status"); Guard.That(i.Area > 0 && i.MonthlyRent >= 0, "invalid_dimensions");
        p.Name = Guard.Text(i.Name, "name"); p.Type = i.Type; p.Address = Guard.Text(i.Address, "address", 500); p.Building = Guard.Text(i.Building, "building"); p.Section = i.Section; p.Floor = i.Floor; p.Area = i.Area; p.MonthlyRent = i.MonthlyRent; p.Description = Guard.Text(i.Description, "description", 10000); p.Utilities = i.Utilities; p.Status = i.Status;
    }
}

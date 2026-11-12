using Cube.Api.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Cube.Api.AdminEndpoints;

namespace Cube.Api;

public static class IdentityAdminEndpoints
{
    public static void MapAdminIdentity(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").AddEndpointFilter<InputValidationFilter>().RequireAuthorization("Admin").WithTags("Administration");
        g.MapGet("/registration-applications", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Users.Where(x => !x.Approved && x.Role == "Tenant").OrderBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(x => new { x.Id, x.UserName, x.Email, x.FullName, x.PhoneNumber, x.RequestedOrganization, x.RequestedInn, x.Blocked }).ToListAsync(ct));
        g.MapPost("/registration-applications/{id:guid}/approve", async (Guid id, Approval input, UserManager<User> users, CubeDb db, Access a, CancellationToken ct) =>
        {
            var u = await users.FindByIdAsync(id.ToString()) ?? throw new ApiError(404, "application_not_found");
            Guard.That(!u.Approved && u.Role == "Tenant", "already_processed", 409);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            Tenant t;
            if (input.ExistingTenantId is Guid tid)
            {
                t = await db.Tenants.FindAsync([tid], ct) ?? throw new ApiError(404, "tenant_not_found");
                Guard.That(t.Inn == u.RequestedInn, "organization_inn_mismatch");
            }
            else
            {
                Guard.That(!await db.Tenants.AnyAsync(x => x.Inn == u.RequestedInn, ct), "existing_tenant_requires_explicit_association", 409);
                t = new Tenant { Name = u.RequestedOrganization, Inn = u.RequestedInn, ContactName = u.FullName, Email = u.Email!, Phone = u.PhoneNumber! };
                db.Tenants.Add(t);
                await db.SaveChangesAsync(ct);
            }
            u.TenantId = t.Id;
            u.Approved = true;
            u.Blocked = false;
            Guard.That((await users.UpdateAsync(u)).Succeeded, "approval_conflict", 409);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = "ApproveRegistration", ResourceId = id });
            Events.Queue(db, "Email", "AccountApproved", id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new
            {
                u.Id,
                u.TenantId,
                Status = "Approved"
            });
        });
        g.MapPut("/users/{id:guid}/blocked", async (Guid id, BlockInput input, UserManager<User> users, Access a, CubeDb db, CancellationToken ct) =>
        {
            Guard.That(id != a.UserId, "cannot_block_self");
            var u = await users.FindByIdAsync(id.ToString()) ?? throw new ApiError(404, "user_not_found");
            u.Blocked = input.Blocked;
            await users.UpdateSecurityStampAsync(u);
            Guard.That((await users.UpdateAsync(u)).Succeeded, "user_update_failed", 409);
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = input.Blocked ? "BlockUser" : "UnblockUser", ResourceId = id });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        g.MapPost("/staff", async (StaffInput input, UserManager<User> users, Access a, CubeDb db, CancellationToken ct) =>
        {
            var u = new User { Id = Guid.NewGuid(), UserName = Guard.Text(input.Login, "login", 100), Email = Guard.Email(input.Email), FullName = Guard.Text(input.FullName, "name"), Role = "Staff", Approved = true };
            Guard.That((await users.CreateAsync(u, input.Password)).Succeeded, "staff_creation_failed");
            db.Audit.Add(new Audit { ActorId = a.UserId, Action = "CreateStaff", ResourceId = u.Id });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/admin/users/{u.Id}", new
            {
                u.Id,
                u.UserName,
                u.Role
            });
        });
        g.MapGet("/users", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Users.OrderBy(x => x.UserName).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).Select(x => new { x.Id, x.UserName, x.FullName, x.Role, x.TenantId, x.Approved, x.Blocked }).ToListAsync(ct));

    }
}

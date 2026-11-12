using Cube.Api.Modules.Billing.Domain;
using Cube.Api.Workflows;
using Microsoft.EntityFrameworkCore;
using static Cube.Api.TenantEndpoints;

namespace Cube.Api;

public static class ListingsTenantEndpoints
{
    public static void MapTenantListings(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").AddEndpointFilter<InputValidationFilter>().RequireAuthorization().WithTags("Tenant portal");
        g.MapGet("/listings", async (string? type, decimal? minArea, decimal? maxArea, decimal? minPrice, decimal? maxPrice, string? building, int? floor, int? page, int? size, CubeDb db, CancellationToken ct) =>
        {
            var q = from l in db.Listings join p in db.Premises on l.PremisesId equals p.Id where l.Published && !l.Archived && (p.Status == "Available" || p.Status == "Reserved") && (type == null || p.Type == type) && (minArea == null || p.Area >= minArea) && (maxArea == null || p.Area <= maxArea) && (minPrice == null || p.MonthlyRent >= minPrice) && (maxPrice == null || p.MonthlyRent <= maxPrice) && (building == null || p.Building == building) && (floor == null || p.Floor == floor) orderby l.CreatedAt descending, l.Id select new { Listing = l, Premises = p };
            return Results.Ok(new
            {
                Total = await q.CountAsync(ct),
                Items = await q.Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)
            });
        });
        g.MapGet("/listings/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => await (from l in db.Listings join p in db.Premises on l.PremisesId equals p.Id where l.Id == id && l.Published && !l.Archived && (p.Status == "Available" || p.Status == "Reserved") select new { Listing = l, Premises = p }).SingleOrDefaultAsync(ct) is { } item ? Results.Ok(item) : Results.NotFound());
        g.MapPost("/listings/{id:guid}/inquiries", async (Guid id, InquiryInput i, RentalInquiryWorkflow workflow, HttpContext context, CancellationToken ct) =>
        {
            var result = await workflow.Handle(id, i.ContactName, i.Phone, i.Email, i.Message, ct);
            return result.ToHttpResult(context, r => r.Created ? Results.Created($"/api/v1/requests/{r.Inquiry.RequestId}", r.Inquiry) : Results.Ok(r.Inquiry));
        });
        g.MapGet("/me/listing-inquiries", async (Access a, int? page, int? size, CancellationToken ct) => await a.Own<ListingInquiry>().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct));
        g.MapGet("/admin/listings", async (CubeDb db, int? page, int? size, CancellationToken ct) => await db.Listings.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Guard.Page(page) - 1) * Guard.Size(size)).Take(Guard.Size(size)).ToListAsync(ct)).RequireAuthorization("Admin");
        g.MapPost("/admin/listings", async (ListingInput i, CubeDb db, CancellationToken ct) => { Guard.That(await db.Premises.AnyAsync(x => x.Id == i.PremisesId, ct), "premises_not_found", 404); var l = new Listing { PremisesId = i.PremisesId, Title = Guard.Text(i.Title, "title"), Description = Guard.Text(i.Description, "description", 10000), Published = i.Published }; db.Listings.Add(l); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/listings/{l.Id}", l); }).RequireAuthorization("Admin");
        g.MapPut("/admin/listings/{id:guid}", async (Guid id, ListingInput i, CubeDb db, CancellationToken ct) => { var l = await db.Listings.FindAsync([id], ct) ?? throw new ApiError(404, "listing_not_found"); Guard.That(l.PremisesId == i.PremisesId, "cannot_change_listing_premises"); l.Title = Guard.Text(i.Title, "title"); l.Description = Guard.Text(i.Description, "description", 10000); l.Published = i.Published; await db.SaveChangesAsync(ct); return Results.Ok(l); }).RequireAuthorization("Admin");
        g.MapDelete("/admin/listings/{id:guid}", async (Guid id, CubeDb db, CancellationToken ct) => { var l = await db.Listings.FindAsync([id], ct) ?? throw new ApiError(404, "listing_not_found"); l.Archived = true; l.Published = false; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization("Admin");
    }
}

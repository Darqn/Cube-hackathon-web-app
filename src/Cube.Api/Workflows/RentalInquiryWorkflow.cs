using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Cube.Api.Infrastructure.Persistence;
using Cube.Api.Modules.Requests.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Workflows;

public sealed record RentalInquiryResult(ListingInquiry Inquiry, bool Created);

// Cross-feature coordination belongs here, rather than a Property -> RequestEndpoints dependency.
public sealed class RentalInquiryWorkflow(ICurrentActor actor, TenantScope tenant, CubeDb db)
{
    public async Task<Outcome<RentalInquiryResult>> Handle(Guid id, string contact, string phone, string email, string message, CancellationToken ct)
    {
        if (actor.IsStaff) return Outcome<RentalInquiryResult>.Reject(Failure.Forbidden("tenant_required"));
        var existing = await db.Inquiries.SingleOrDefaultAsync(x => x.TenantId == tenant.TenantId && x.ListingId == id, ct);
        if (existing is not null) return Outcome<RentalInquiryResult>.Success(new(existing, false));
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == id && x.Published && !x.Archived, ct);
        if (listing is null) return Outcome<RentalInquiryResult>.Reject(Failure.NotFound("listing_not_found"));
        if (!await db.Premises.AnyAsync(x => x.Id == listing.PremisesId && x.Status == "Available", ct))
            return Outcome<RentalInquiryResult>.Reject(Failure.Conflict("listing_unavailable"));
        var category = await db.Categories.SingleAsync(x => x.Code == "rent", ct);
        if (string.IsNullOrWhiteSpace(message) || message.Length > 10000)
            return Outcome<RentalInquiryResult>.Reject(Failure.Invalid("invalid_message"));
        var created = RequestFactory.Create(new(null, $"Rental inquiry: {listing.Title}", message.Trim(), contact, phone, email), tenant.TenantId, category);
        if (created.Error is { } error) return Outcome<RentalInquiryResult>.Reject(error);

        var request = created.Value;
        db.Requests.Add(request);
        await Events.NotifyStaffAsync(db, request.AssigneeId, request.TenantId, "StaffRequestAssigned", request.Id, "Новая заявка на помещение: " + request.Subject, ct);
        var inquiry = new ListingInquiry { TenantId = tenant.TenantId, ListingId = id, RequestId = request.Id };
        db.Inquiries.Add(inquiry);
        Events.Queue(db, "Bitrix", "RequestCreated", request.Id);
        await db.SaveChangesAsync(ct);
        return Outcome<RentalInquiryResult>.Success(new(inquiry, true));
    }
}

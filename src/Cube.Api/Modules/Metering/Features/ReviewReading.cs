using Cube.Api.Common.Identity;
using Cube.Api.Common.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Modules.Metering.Features;

public sealed record ReadingReview(Guid Id, string Status, string ReviewReason);

public sealed class ReviewReadingHandler(ICurrentActor actor, MeteringVisibility visibility, CubeDb db)
{
    public async Task<Outcome<ReadingReview>> Handle(Guid id, string status, string reason, CancellationToken ct)
    {
        if (status is not "Accepted" and not "Rejected") return Outcome<ReadingReview>.Reject(Failure.Invalid("invalid_status"));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) return Outcome<ReadingReview>.Reject(Failure.Invalid("invalid_reason"));
        var reading = await visibility.Readings().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (reading is null) return Outcome<ReadingReview>.Reject(Failure.NotFound("reading_not_found"));
        var trimmedReason = reason.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await visibility.Readings().Where(x => x.Id == id && x.Status == "Submitted")
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, status).SetProperty(x => x.ReviewReason, trimmedReason), ct);
        if (changed != 1) return Outcome<ReadingReview>.Reject(Failure.Conflict("already_reviewed"));

        await Events.NotifyAsync(db, reading.TenantId, "ReadingReviewed", id, trimmedReason, ct);
        db.Audit.Add(new Audit { ActorId = actor.UserId!.Value, Action = "ReviewReading", ResourceId = id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Outcome<ReadingReview>.Success(new ReadingReview(id, status, trimmedReason));
    }
}

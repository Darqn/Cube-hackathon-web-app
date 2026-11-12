using Cube.Api;
using Cube.Api.Common.Identity;
using Cube.Api.Infrastructure.Persistence;
using Cube.Api.Modules.Property;
using Cube.Api.Modules.Requests;
using Cube.Api.Modules.Requests.Domain;
using Cube.Api.Modules.Requests.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cube.Tests;

public sealed class ScenarioTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Actor(Guid? UserId, Guid? TenantId, bool IsAdmin = false, bool IsStaff = false) : ICurrentActor;
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Create_handler_runs_without_http_and_commits_request_outbox_and_actor_audit()
    {
        using var scope = fixture.Services.CreateScope();
        var actor = new Actor(fixture.UserA, fixture.TenantA);
        await using var db = new CubeDb(scope.ServiceProvider.GetRequiredService<DbContextOptions<CubeDb>>(), actor);
        var handler = new CreateRequestHandler(actor, new TenantScope(actor, db), new ActiveLeases(db, TimeProvider.System), db);
        var outcome = await handler.Handle(fixture.Category, new RequestContent(fixture.PremisesA, "Direct scenario", "Description", "Contact", "1", "a@example.test"), default);
        Assert.Null(outcome.Error);
        var id = outcome.Value.Id;
        db.ChangeTracker.Clear();
        Assert.True(await db.Requests.AnyAsync(x => x.Id == id && x.Subject == "Direct scenario"));
        Assert.True(await db.Outbox.AnyAsync(x => x.ResourceId == id && x.Kind == "RequestCreated"));
        Assert.True(await db.Audit.AnyAsync(x => x.ResourceId == id && x.ActorId == fixture.UserA));
    }

    [Theory]
    [InlineData("2025-12-31", false)]
    [InlineData("2026-01-01", true)]
    [InlineData("2030-01-01", true)]
    [InlineData("2030-01-02", false)]
    public async Task Active_lease_rule_uses_utc_and_includes_both_boundary_days(string date, bool expected)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        var clock = new Clock(DateTimeOffset.Parse(date + "T12:00:00Z"));
        Assert.Equal(expected, await new ActiveLeases(db, clock).Contains(fixture.TenantA, fixture.PremisesA, default));
    }

    [Theory]
    [InlineData("Closed", "reason", "invalid_transition")]
    [InlineData("Accepted", " ", "invalid_reason")]
    public async Task Rejected_status_change_leaves_history_and_side_effects_unchanged(string status, string reason, string code)
    {
        using var scope = fixture.Services.CreateScope();
        var actor = new Actor(fixture.Staff, null, IsStaff: true);
        await using var db = new CubeDb(scope.ServiceProvider.GetRequiredService<DbContextOptions<CubeDb>>(), actor);
        var request = new ServiceRequest { TenantId = fixture.TenantA, CategoryId = fixture.Category, AssigneeId = fixture.Staff, Subject = "Rejected status" };
        db.Add(request);
        await db.SaveChangesAsync();
        var visibility = new RequestVisibility(actor, db, new TenantScope(actor, db));
        var outcome = await new ChangeRequestStatusHandler(actor, visibility, db).Handle(request.Id, status, reason, request.Version, default);
        Assert.Equal(code, outcome.Error?.Code);
        db.ChangeTracker.Clear();
        Assert.Equal("New", (await db.Requests.FindAsync(request.Id))!.Status);
        Assert.False(await db.RequestEvents.AnyAsync(x => x.RequestId == request.Id));
        Assert.False(await db.Outbox.AnyAsync(x => x.ResourceId == request.Id));
        Assert.False(await db.Notifications.AnyAsync(x => x.ResourceId == request.Id));
    }
}

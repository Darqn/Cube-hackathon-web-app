using Cube.Api;
using Cube.Api.Modules.Notifications.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cube.Tests;

public sealed class SchedulerTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2027, 11, 25, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task Scheduler_respects_preferences_blocking_and_retry_deduplication()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        var clock = new Clock();
        var user = await db.Users.SingleAsync(x => x.Id == fixture.UserA);
        user.NewsNotifications = user.InvoiceReminders = user.MeterReminders = false;
        var news = new News { Title = "Scheduler", AllTenants = true, PublishAt = clock.GetUtcNow().AddMinutes(-1) };
        db.Add(news);
        await db.SaveChangesAsync();
        var scheduler = new NotificationScheduler(db, clock);
        await scheduler.Tick(default);
        await scheduler.Tick(default);
        Assert.False(await db.Notifications.AnyAsync(x => x.UserId == fixture.UserA));
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == fixture.UserB && x.ResourceId == news.Id));
        user.NewsNotifications = user.InvoiceReminders = user.MeterReminders = true;
        (await db.Tenants.FindAsync(fixture.TenantA))!.Blocked = true;
        await db.SaveChangesAsync();
        await scheduler.Tick(default);
        Assert.False(await db.Notifications.AnyAsync(x => x.UserId == fixture.UserA));
    }
}

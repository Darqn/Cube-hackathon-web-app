using System.Security.Claims;
using Cube.Api;
using Cube.Api.Common.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cube.Tests;

public sealed class CurrentActorTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Actor_resolved_before_authentication_observes_the_principal_and_audits_the_change()
    {
        using var scope = fixture.Services.CreateScope();
        var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        Assert.Null(actor.UserId);
        Assert.Null(actor.TenantId);
        Assert.False(actor.IsStaff);
        try
        {
            http.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, fixture.UserA.ToString()),
                    new Claim("tenant", fixture.TenantA.ToString()),
                    new Claim(ClaimTypes.Role, "Tenant")], "Session"))
            };
            Assert.Equal(fixture.UserA, actor.UserId);
            Assert.Equal(fixture.TenantA, actor.TenantId);
            var request = new ServiceRequest { TenantId = fixture.TenantA, CategoryId = fixture.Category, Subject = "Actor audit" };
            db.Add(request);
            await db.SaveChangesAsync();
            Assert.True(await db.Audit.AnyAsync(x => x.ResourceId == request.Id && x.ActorId == fixture.UserA));
        }
        finally
        {
            http.HttpContext = null;
        }
        Assert.Null(actor.UserId);
    }

    [Fact]
    public async Task Background_scope_does_not_invent_an_audit_actor()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        var request = new ServiceRequest { TenantId = fixture.TenantA, CategoryId = fixture.Category, Subject = "Background audit" };
        db.Add(request);
        await db.SaveChangesAsync();
        Assert.False(await db.Audit.AnyAsync(x => x.ResourceId == request.Id));
    }
}

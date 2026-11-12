using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Api.Hosting;

internal static class MaintenanceCommands
{
    public static async Task<bool> RunMaintenanceCommand(this WebApplication app, string[] args)
    {
        if (args.Contains("--seed-demo"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            await DemoSeed.Run(scope.ServiceProvider, app.Environment, app.Configuration);
            return true;
        }
        if (args.Contains("--migrate"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            await db.Database.MigrateAsync();
            await Seed.Categories(db);
            return true;
        }
        if (args.Contains("--bootstrap-admin"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
            if (await db.Users.AnyAsync(x => x.Role == "Admin"))
                throw new InvalidOperationException("An administrator already exists; bootstrap refused");
            var login = app.Configuration["Bootstrap:Login"] ?? throw new InvalidOperationException("Bootstrap__Login required");
            var email = app.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Bootstrap__Email required");
            var password = app.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Bootstrap__Password required");
            var result = await users.CreateAsync(new User { Id = Guid.NewGuid(), UserName = login, Email = Guard.Email(email), FullName = "Administrator", Approved = true, Role = "Admin" }, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Bootstrap failed: " + string.Join(',', result.Errors.Select(x => x.Code)));
            return true;
        }
        return false;
    }
}

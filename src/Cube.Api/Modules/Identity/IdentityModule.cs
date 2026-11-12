using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Cube.Api.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddIdentityCore<User>(o => { o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 12; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); }).AddEntityFrameworkStores<CubeDb>().AddDefaultTokenProviders();
        services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionHandler>("Session", _ => { });
        services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireRole("Admin")));
        return services;
    }
}

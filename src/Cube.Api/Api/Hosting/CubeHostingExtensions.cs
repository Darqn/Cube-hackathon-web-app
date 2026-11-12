using System.Threading.RateLimiting;
using Cube.Api.Api.Authentication;
using Cube.Api.Api.Middleware;
using Cube.Api.Common.Identity;
using Cube.Api.Infrastructure;
using Cube.Api.Modules.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Cube.Api.Api.Hosting;

public static class CubeHostingExtensions
{
    public static WebApplicationBuilder AddCubeServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddIdentityModule();
        builder.Services.AddScoped<Access>();
        builder.Services.AddScoped<PaymentLedger>();
        builder.Services.AddOpenApi(); builder.Services.AddProblemDetails();
        builder.Services.AddRateLimiter(o =>
        {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("RateLimits:AuthPermitLimit") ?? 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 21 * 1024 * 1024);
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 21 * 1024 * 1024);
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => { if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod(); }));
        if (builder.Configuration.GetValue<bool>("Notifications:WorkerEnabled")) builder.Services.AddHostedService<NotificationWorker>();
        return builder;
    }

    public static WebApplication UseCubeHttp(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<ApiErrorMiddleware>();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseCors();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
        return app;
    }
}

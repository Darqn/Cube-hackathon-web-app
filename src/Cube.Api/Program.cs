using System.Threading.RateLimiting;
using Cube.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<CubeDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Cube") ?? throw new InvalidOperationException("ConnectionStrings__Cube is required")));
builder.Services.AddIdentityCore<User>(o => { o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 12; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); }).AddEntityFrameworkStores<CubeDb>().AddDefaultTokenProviders();
builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionHandler>("Session", _ => { });
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireRole("Admin")));
builder.Services.AddScoped<Access>(); builder.Services.AddScoped<PaymentLedger>();
builder.Services.AddSingleton<UnconfiguredIntegrations>();
builder.Services.AddSingleton<IBitrixGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
builder.Services.AddSingleton<IAccountingGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
builder.Services.AddSingleton<IPaymentGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
builder.Services.AddSingleton<IEmailGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
builder.Services.AddSingleton<PrivateStorage>();
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
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    try { await next(context); }
    catch (Exception e) when (e is ApiError or DbUpdateException or BadHttpRequestException)
    {
        var status = e switch { ApiError a => a.Status, DbUpdateConcurrencyException => 409, DbUpdateException => 409, BadHttpRequestException b => b.StatusCode, _ => 500 };
        var code = e is ApiError a2 ? a2.Message : e is DbUpdateException ? "database_constraint_or_concurrency_conflict" : "invalid_request";
        if (!context.Response.HasStarted) await Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok(new { Status = "Alive" }));
app.MapGet("/health/ready", async (CubeDb db, CancellationToken ct) => { try { return await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any() ? Results.Ok(new { Status = "Ready" }) : Results.StatusCode(503); } catch { return Results.StatusCode(503); } });
app.MapAuth(); app.MapStaff(); app.MapAdmin(); app.MapRequests(); app.MapTenantFeatures(); app.MapNews(); app.MapFiles(); app.MapMedia(); app.MapDetails();

if (args.Contains("--seed-demo"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DemoSeed.Run(scope.ServiceProvider, app.Environment, builder.Configuration);
    return;
}
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>(); await db.Database.MigrateAsync(); await Seed.Categories(db); return;
}
if (args.Contains("--bootstrap-admin"))
{
    await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>(); var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
    if (await db.Users.AnyAsync(x => x.Role == "Admin")) throw new InvalidOperationException("An administrator already exists; bootstrap refused");
    var login = builder.Configuration["Bootstrap:Login"] ?? throw new InvalidOperationException("Bootstrap__Login required");
    var email = builder.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Bootstrap__Email required");
    var password = builder.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Bootstrap__Password required");
    var result = await users.CreateAsync(new User { Id = Guid.NewGuid(), UserName = login, Email = Guard.Email(email), FullName = "Administrator", Approved = true, Role = "Admin" }, password);
    if (!result.Succeeded) throw new InvalidOperationException("Bootstrap failed: " + string.Join(',', result.Errors.Select(x => x.Code))); return;
}
await app.RunAsync();
public partial class Program;

using Cube.Api;
using Cube.Api.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddCubeServices();

var app = builder.Build();
app.UseCubeHttp();
app.MapHealth();
app.MapAuth();
app.MapStaffDashboard();
app.MapStaffNotifications();
app.MapRequestReplies();
app.MapStaffMetering();
app.MapAdminIdentity();
app.MapAdminProperty();
app.MapAdminMetering();
app.MapAdminRouting();
app.MapAdminDiagnostics();
app.MapRequests();
app.MapMetering();
app.MapTenantProperty();
app.MapTenantBilling();
app.MapTenantListings();
app.MapTenantNotifications();
app.MapTenantDashboard();
app.MapNews();
app.MapFiles();
app.MapRequestAttachments();
app.MapMedia();
app.MapPropertyDetails();
app.MapMeteringDetails();
app.MapIdentityDetails();
app.MapDocumentsDetails();
app.MapBillingDetails();
app.MapNewsDetails();

if (await app.RunMaintenanceCommand(args)) return;
await app.RunAsync();

public partial class Program;

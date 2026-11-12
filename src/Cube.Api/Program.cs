using Cube.Api;
using Cube.Api.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddCubeServices();

var app = builder.Build();
app.UseCubeHttp();
app.MapHealth();
app.MapAuth();
app.MapStaff();
app.MapRequestReplies();
app.MapStaffMetering();
app.MapAdmin();
app.MapRequests();
app.MapMetering();
app.MapTenantFeatures();
app.MapNews();
app.MapFiles();
app.MapRequestAttachments();
app.MapMedia();
app.MapDetails();

if (await app.RunMaintenanceCommand(args)) return;
await app.RunAsync();

public partial class Program;

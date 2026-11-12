using Cube.Api.Modules.Metering.Features;
using Cube.Api.Modules.Metering.Infrastructure;

namespace Cube.Api.Modules.Metering;

public static class MeteringModule
{
    public static IServiceCollection AddMeteringModule(this IServiceCollection services) => services
        .AddSingleton<MeterOcr>()
        .AddScoped<MeteringVisibility>()
        .AddScoped<SubmitReadingHandler>()
        .AddScoped<ReviewReadingHandler>();
}

using Cube.Api.Modules.Metering.Features;

namespace Cube.Api.Modules.Metering;

public static class MeteringModule
{
    public static IServiceCollection AddMeteringModule(this IServiceCollection services) => services
        .AddScoped<MeteringVisibility>()
        .AddScoped<SubmitReadingHandler>()
        .AddScoped<ReviewReadingHandler>();
}

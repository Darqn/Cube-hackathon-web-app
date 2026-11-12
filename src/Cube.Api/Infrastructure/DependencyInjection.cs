using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CubeDb>(o => o.UseNpgsql(configuration.GetConnectionString("Cube") ?? throw new InvalidOperationException("ConnectionStrings__Cube is required")));
        services.AddSingleton<UnconfiguredIntegrations>();
        services.AddSingleton<IBitrixGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
        services.AddSingleton<IAccountingGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
        services.AddSingleton<IPaymentGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
        services.AddSingleton<IEmailGateway>(s => s.GetRequiredService<UnconfiguredIntegrations>());
        services.AddSingleton<PrivateStorage>();
        return services;
    }
}

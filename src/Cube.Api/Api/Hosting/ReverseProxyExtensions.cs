using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Cube.Api.Api.Hosting;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddCubeReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ReverseProxy:Enabled")) return services;
        var configured = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        if (configured.Length == 0)
            throw new InvalidOperationException("ReverseProxy:KnownProxies is required when proxy support is enabled.");
        var proxies = configured.Select(IPAddress.Parse).ToArray();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
        });
        services.AddHttpsRedirection(options => options.HttpsPort = 443);
        return services;
    }
}

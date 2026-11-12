using System.Net;
using Cube.Api.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cube.Tests;

public sealed class ReverseProxyTests
{
    [Theory]
    [InlineData(true, "172.30.231.10", "https", "198.51.100.42", 200)]
    [InlineData(true, "203.0.113.10", "http", "203.0.113.10", 307)]
    [InlineData(false, "172.30.231.10", "http", "172.30.231.10", 200)]
    public async Task Only_explicitly_trusted_proxies_can_supply_scheme_and_client_ip(
        bool enabled, string remote, string expectedScheme, string expectedClient, int expectedStatus)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Cube"] = "Host=unused;Database=cube;Username=unused",
            ["ReverseProxy:Enabled"] = enabled.ToString(),
            ["ReverseProxy:KnownProxies:0"] = "172.30.231.10"
        });
        builder.AddCubeServices();
        await using var app = builder.Build();
        app.UseCubeHttp();
        app.MapGet("/probe", () => Results.Ok());
        await app.StartAsync();
        var result = await app.GetTestServer().SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("api.example.test");
            context.Request.Path = "/probe";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.42";
        });
        Assert.Equal(expectedScheme, result.Request.Scheme);
        Assert.Equal(expectedClient, result.Connection.RemoteIpAddress!.ToString());
        Assert.Equal(expectedStatus, result.Response.StatusCode);
        if (expectedStatus == 307) Assert.Equal("https://api.example.test/probe", result.Response.Headers.Location.ToString());
    }

    [Fact]
    public void Proxy_mode_requires_an_explicit_trusted_address()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Cube"] = "Host=unused;Database=cube;Username=unused",
            ["ReverseProxy:Enabled"] = "true"
        });
        Assert.Throws<InvalidOperationException>(() => builder.AddCubeServices());
    }
}

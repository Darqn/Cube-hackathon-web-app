using Cube.Api.Modules.Requests.Features;

namespace Cube.Api.Modules.Requests;

public static class RequestsModule
{
    public static IServiceCollection AddRequestsModule(this IServiceCollection services) => services
        .AddScoped<RequestVisibility>()
        .AddScoped<CreateRequestHandler>()
        .AddScoped<ChangeRequestStatusHandler>()
        .AddScoped<AddRequestMessageHandler>()
        .AddScoped<AddRequestAttachmentHandler>()
        .AddScoped<ReplyToRequestHandler>()
        .AddScoped<AssignRequestHandler>();
}

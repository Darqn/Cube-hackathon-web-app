using Cube.Api.Modules.Notifications.Features;

namespace Cube.Api.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) => services
        .AddScoped<StaffNotificationVisibility>()
        .AddScoped<NotificationScheduler>();
}

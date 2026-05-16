using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace Cloud.Shared.Extensions;

public static class SignalRJwtExtensions
{
    /// <summary>
    /// Configures JWT bearer authentication to accept tokens passed via the
    /// <c>?access_token=</c> query string parameter for SignalR WebSocket connections
    /// on the specified hub path.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="hubPath">The SignalR hub path to apply query-string token extraction to (e.g. "/web-status").</param>
    public static IServiceCollection AddSignalRJwtFromQueryString(this IServiceCollection services, string hubPath)
    {
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure(options =>
            {
                var existingOnMessageReceived = options.Events?.OnMessageReceived;
                options.Events ??= new JwtBearerEvents();
                options.Events.OnMessageReceived = async context =>
                {
                    if (existingOnMessageReceived != null)
                        await existingOnMessageReceived(context);

                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(hubPath))
                    {
                        context.Token = accessToken;
                    }
                };
            });

        return services;
    }
}

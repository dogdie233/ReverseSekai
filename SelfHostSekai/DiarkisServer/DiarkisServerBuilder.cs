using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DiarkisServer.Server;
using DiarkisServer.Services;

namespace DiarkisServer;

public class DiarkisServerOptions
{
    public string Host { get; set; } = "0.0.0.0";
    public int UdpPort { get; set; } = 7100;
}

public static class DiarkisServerBuilder
{
    /// <summary>
    /// Register DiarkisServer services into the DI container.
    /// </summary>
    public static IServiceCollection AddDiarkisServer(
        this IServiceCollection services, Action<DiarkisServerOptions>? configure = null)
    {
        var options = new DiarkisServerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddMemoryCache();
        services.AddSingleton<IRoomService, RoomService>();
        services.AddSingleton<IMatchmakingService, MatchmakingService>();
        services.AddSingleton<DiarkisRealtimeServer>();

        return services;
    }

    /// <summary>
    /// Start the UDP listener as a background service.
    /// Call this after building the host.
    /// </summary>
    public static IHost UseDiarkisServer(this IHost host)
    {
        var server = host.Services.GetRequiredService<DiarkisRealtimeServer>();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.ApplicationStarted.Register(() => server.Start());
        lifetime.ApplicationStopping.Register(() => server.Stop());

        return host;
    }
}

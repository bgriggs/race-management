using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cloud.Shared.RedMist;

public static class RedMistServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RedMist token provider, REST client, and supporting <see cref="HttpClient"/>s.
    /// Bind <see cref="RedMistOptions"/> from the <c>RedMist</c> configuration section.
    /// </summary>
    public static IServiceCollection AddRedMistClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedMistOptions>(configuration.GetSection(RedMistOptions.SectionName));
        services.AddHttpClient("redmist-auth");
        services.AddHttpClient("redmist-api");
        services.AddSingleton<IRedMistTokenProvider, RedMistTokenProvider>();
        services.AddSingleton<IRedMistRestClient, RedMistRestClient>();
        return services;
    }
}

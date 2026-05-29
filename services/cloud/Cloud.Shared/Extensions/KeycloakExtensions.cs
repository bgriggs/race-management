using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Authorization;
using Keycloak.AuthServices.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cloud.Shared.Extensions;

public static class KeycloakExtensions
{
    /// <summary>
    /// Registers Keycloak JWT authentication, realm-role authorization, and a Keycloak OIDC health check.
    /// </summary>
    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeycloakWebApiAuthentication(configuration);
        services.AddAuthorization().AddKeycloakAuthorization(options =>
        {
            options.EnableRolesMapping = RolesClaimTransformationSource.Realm;
            // Note, this should correspond to role configured with KeycloakAuthenticationOptions
            options.RoleClaimType = KeycloakConstants.RoleClaimType;
        });

        var authServerUrl = configuration["Keycloak:AuthServerUrl"] ?? throw new ArgumentNullException("Keycloak:AuthServerUrl");
        var realm = configuration["Keycloak:Realm"] ?? throw new ArgumentNullException("Keycloak:Realm");
        // Keycloak's OIDC discovery endpoint is realm-scoped; the health check appends
        // /.well-known/openid-configuration, so the URI must include /realms/{realm}.
        var discoveryUri = new Uri($"{authServerUrl.TrimEnd('/')}/realms/{realm}");
        services.AddHealthChecks()
            .AddOpenIdConnectServer(discoveryUri, name: "keycloak", tags: ["auth", "keycloak"]);

        return services;
    }
}

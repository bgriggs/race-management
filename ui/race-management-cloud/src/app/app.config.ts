import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  provideKeycloak,
  includeBearerTokenInterceptor,
  INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
  IncludeBearerTokenCondition,
} from 'keycloak-angular';

import { routes } from './app.routes';
import { APP_CONFIG, AppConfig } from './config/app-config';

export function buildAppConfig(config: AppConfig): ApplicationConfig {
  const apiBearerCondition: IncludeBearerTokenCondition = {
    urlPattern: new RegExp(`^${escapeRegex(config.api.baseUrl)}(/.*)?$`, 'i'),
  };

  return {
    providers: [
      { provide: APP_CONFIG, useValue: config },
      provideBrowserGlobalErrorListeners(),
      provideRouter(routes),
      provideHttpClient(withInterceptors([includeBearerTokenInterceptor])),
      provideKeycloak({
        config: {
          url: config.keycloak.url,
          realm: config.keycloak.realm,
          clientId: config.keycloak.clientId,
        },
        initOptions: {
          onLoad: 'check-sso',
          silentCheckSsoRedirectUri:
            window.location.origin + '/silent-check-sso.html',
          pkceMethod: 'S256',
          checkLoginIframe: false,
        },
        providers: [
          {
            provide: INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
            useValue: [apiBearerCondition],
          },
        ],
      }),
    ],
  };
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

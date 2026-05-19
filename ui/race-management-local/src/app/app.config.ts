import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import {
  INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
  IncludeBearerTokenCondition,
  includeBearerTokenInterceptor,
  provideKeycloak
} from 'keycloak-angular';
import {
  MANAGEMENT_DATA_CLIENT,
  type ManagementDataClient
} from '../../../shared-ui/src/lib/data/management-data-client';
import {
  MANAGEMENT_DATA_CLIENT_SETTINGS,
  type ManagementDataClientSettings
} from '../../../shared-ui/src/lib/data/management-data-client-settings';
import { SiteSettingsService } from './config/site-settings';
import { LocalManagementDataClient } from './data/local-management-data-client';

import { routes } from './app.routes';

export function buildAppConfig(siteSettings: SiteSettingsService): ApplicationConfig {
  const settings = siteSettings.value;
  const apiBearerCondition: IncludeBearerTokenCondition = {
    urlPattern: new RegExp(`^${escapeRegex(settings.managementDataServiceBaseUrl)}(/.*)?$`, 'i')
  };

  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideRouter(routes),
      provideHttpClient(withInterceptors([includeBearerTokenInterceptor])),
      provideKeycloak({
        config: {
          url: settings.keycloak.url,
          realm: settings.keycloak.realm,
          clientId: settings.keycloak.clientId
        },
        initOptions: {
          onLoad: 'check-sso',
          silentCheckSsoRedirectUri: window.location.origin + '/silent-check-sso.html',
          pkceMethod: 'S256',
          checkLoginIframe: false
        },
        providers: [
          {
            provide: INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
            useValue: [apiBearerCondition]
          }
        ]
      }),
      { provide: SiteSettingsService, useValue: siteSettings },
      {
        provide: MANAGEMENT_DATA_CLIENT_SETTINGS,
        useValue: {
          baseServerUrl: settings.managementDataServiceBaseUrl
        } satisfies ManagementDataClientSettings
      },
      {
        provide: MANAGEMENT_DATA_CLIENT,
        deps: [LocalManagementDataClient],
        useFactory: (localManagementDataClient: LocalManagementDataClient): ManagementDataClient => {
          return localManagementDataClient;
        }
      },
      LocalManagementDataClient
    ]
  };
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

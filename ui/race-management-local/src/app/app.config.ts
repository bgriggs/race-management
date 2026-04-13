import { provideHttpClient } from '@angular/common/http';
import { APP_INITIALIZER, ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
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

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    {
      provide: APP_INITIALIZER,
      multi: true,
      deps: [SiteSettingsService],
      useFactory: (siteSettingsService: SiteSettingsService) => {
        return () => siteSettingsService.loadAsync();
      }
    },
    {
      provide: MANAGEMENT_DATA_CLIENT_SETTINGS,
      deps: [SiteSettingsService],
      useFactory: (siteSettingsService: SiteSettingsService): ManagementDataClientSettings => {
        return {
          baseServerUrl: siteSettingsService.value.managementDataServiceBaseUrl
        };
      }
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

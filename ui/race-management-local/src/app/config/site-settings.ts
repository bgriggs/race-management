import { Injectable } from '@angular/core';

export interface KeycloakSettings {
  url: string;
  realm: string;
  clientId: string;
}

export interface SiteSettings {
  managementDataServiceBaseUrl: string;
  keycloak: KeycloakSettings;
}

const defaultSiteSettings: SiteSettings = {
  managementDataServiceBaseUrl: 'http://localhost:5206',
  keycloak: {
    url: 'https://auth.redmist.racing',
    realm: 'race-management',
    clientId: 'race-management-ui'
  }
};

@Injectable({ providedIn: 'root' })
export class SiteSettingsService {
  private settings: SiteSettings = defaultSiteSettings;

  async loadAsync(): Promise<SiteSettings> {
    try {
      const response = await fetch('/site-settings.json', { cache: 'no-store' });
      if (response.ok) {
        const loadedSettings = (await response.json()) as Partial<SiteSettings>;
        this.settings = {
          ...defaultSiteSettings,
          ...loadedSettings,
          keycloak: {
            ...defaultSiteSettings.keycloak,
            ...(loadedSettings.keycloak ?? {})
          }
        };
      }
    } catch {
      this.settings = defaultSiteSettings;
    }
    return this.settings;
  }

  get value(): SiteSettings {
    return this.settings;
  }
}

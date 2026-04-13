import { Injectable } from '@angular/core';

export interface SiteSettings {
  managementDataServiceBaseUrl: string;
}

const defaultSiteSettings: SiteSettings = {
  managementDataServiceBaseUrl: 'http://localhost:5206'
};

@Injectable({ providedIn: 'root' })
export class SiteSettingsService {
  private settings: SiteSettings = defaultSiteSettings;

  async loadAsync(): Promise<void> {
    try {
      const response = await fetch('/site-settings.json');
      if (!response.ok) {
        return;
      }

      const loadedSettings = (await response.json()) as Partial<SiteSettings>;
      this.settings = {
        ...defaultSiteSettings,
        ...loadedSettings
      };
    } catch {
      this.settings = defaultSiteSettings;
    }
  }

  get value(): SiteSettings {
    return this.settings;
  }
}

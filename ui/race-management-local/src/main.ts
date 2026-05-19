import { bootstrapApplication } from '@angular/platform-browser';
import { buildAppConfig } from './app/app.config';
import { App } from './app/app';
import { SiteSettingsService } from './app/config/site-settings';

const siteSettings = new SiteSettingsService();
siteSettings
  .loadAsync()
  .then(() => bootstrapApplication(App, buildAppConfig(siteSettings)))
  .catch((err: unknown) => console.error('Application bootstrap failed:', err));

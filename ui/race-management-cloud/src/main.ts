import { bootstrapApplication } from '@angular/platform-browser';
import { buildAppConfig } from './app/app.config';
import { App } from './app/app';
import { loadAppConfig } from './app/config/app-config';

loadAppConfig()
  .then((config) => bootstrapApplication(App, buildAppConfig(config)))
  .catch((err: unknown) => console.error('Application bootstrap failed:', err));

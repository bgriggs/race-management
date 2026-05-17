import { InjectionToken } from '@angular/core';

export interface KeycloakConfig {
  url: string;
  realm: string;
  clientId: string;
}

export interface ApiConfig {
  baseUrl: string;
}

export interface AppConfig {
  keycloak: KeycloakConfig;
  api: ApiConfig;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

const LOCAL_OVERRIDE = '/config.local.json';
const DEFAULT_CONFIG = '/config.json';

export async function loadAppConfig(): Promise<AppConfig> {
  const local = await tryFetch(LOCAL_OVERRIDE);
  if (local) return local;

  const base = await tryFetch(DEFAULT_CONFIG);
  if (!base) {
    throw new Error(`Failed to load ${DEFAULT_CONFIG}`);
  }
  return base;
}

async function tryFetch(path: string): Promise<AppConfig | null> {
  try {
    const res = await fetch(path, { cache: 'no-store' });
    if (!res.ok) return null;
    return (await res.json()) as AppConfig;
  } catch {
    return null;
  }
}

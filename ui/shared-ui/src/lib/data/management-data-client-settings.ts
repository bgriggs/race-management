import { InjectionToken } from '@angular/core';

export interface ManagementDataClientSettings {
  baseServerUrl: string;
}

export const MANAGEMENT_DATA_CLIENT_SETTINGS = new InjectionToken<ManagementDataClientSettings>(
  'MANAGEMENT_DATA_CLIENT_SETTINGS'
);

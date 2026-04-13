import { InjectionToken } from '@angular/core';
import { CarConfiguration } from '../../models/car-configuration';
import { CarConfigurationSummary } from '../../models/car-configuration-summary';

export interface ManagementDataClient {
  loadCarConfigurationSummariesAsync(): Promise<CarConfigurationSummary[]>;
  loadCarConfigurationAsync(configId: string): Promise<CarConfiguration>;
  saveCarConfigurationAsync(carConfiguration: CarConfiguration): Promise<CarConfiguration>;
  transmitToCarAsync(carConfiguration: CarConfiguration): Promise<CarConfiguration>;
  deleteCarConfigurationAsync(id: string): Promise<void>;
}

export const MANAGEMENT_DATA_CLIENT = new InjectionToken<ManagementDataClient>(
  'MANAGEMENT_DATA_CLIENT'
);

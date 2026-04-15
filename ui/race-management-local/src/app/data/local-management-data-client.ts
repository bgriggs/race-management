import { HttpClient, HttpParams } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  MANAGEMENT_DATA_CLIENT_SETTINGS,
  type ManagementDataClientSettings
} from '../../../../shared-ui/src/lib/data/management-data-client-settings';
import { ManagementDataClient } from '../../../../shared-ui/src/lib/data/management-data-client';
import { CarConfiguration } from '../../../../shared-ui/src/models/car-configuration';
import { CarConfigurationSummary } from '../../../../shared-ui/src/models/car-configuration-summary';
import { ChannelDefinition } from '../../../../shared-ui/src/models/channel-definition';

@Injectable()
export class LocalManagementDataClient implements ManagementDataClient {
  constructor(
    private readonly httpClient: HttpClient,
    @Inject(MANAGEMENT_DATA_CLIENT_SETTINGS)
    private readonly settings: ManagementDataClientSettings
  ) {}

  async loadCarConfigurationSummariesAsync(): Promise<CarConfigurationSummary[]> {
    return await firstValueFrom(
      this.httpClient.get<CarConfigurationSummary[]>(
          this.buildActionUrl('load-car-configuration-summaries')
      )
    );
  }

  async loadReservedChannelDefinitionsAsync(): Promise<ChannelDefinition[]> {
    return await firstValueFrom(
      this.httpClient.get<ChannelDefinition[]>(
        this.buildActionUrl('load-reserved-channel-definitions')
      )
    );
  }

  async loadCarConfigurationAsync(configId: string): Promise<CarConfiguration> {
    return await firstValueFrom(
        this.httpClient.get<CarConfiguration>(this.buildActionUrl('load-car-configuration'), {
        params: new HttpParams().set('configId', configId)
      })
    );
  }

  async saveCarConfigurationAsync(carConfiguration: CarConfiguration): Promise<CarConfiguration> {
    return await firstValueFrom(
        this.httpClient.post<CarConfiguration>(
          this.buildActionUrl('save-car-configuration'),
          carConfiguration
        )
    );
  }

  async transmitToCarAsync(carConfiguration: CarConfiguration): Promise<CarConfiguration> {
    return await firstValueFrom(
      this.httpClient.post<CarConfiguration>(this.buildActionUrl('transmit-to-car'), carConfiguration)
    );
  }

  async deleteCarConfigurationAsync(id: string): Promise<void> {
    return await firstValueFrom(
        this.httpClient.delete<void>(this.buildActionUrl('delete-car-configuration'), {
        params: new HttpParams().set('id', id)
      })
    );
  }

  private buildActionUrl(action: string): string {
    const trimmedBaseUrl = this.settings.baseServerUrl.replace(/\/$/, '');
      return `${trimmedBaseUrl}/v1.0/data/${action}`;
  }
}

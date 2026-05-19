import { HttpClient, HttpParams } from '@angular/common/http';
import { Inject, Injectable, Injector } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  MANAGEMENT_DATA_CLIENT_SETTINGS,
  type ManagementDataClientSettings
} from '../../../../shared-ui/src/lib/data/management-data-client-settings';
import { ManagementDataClient } from '../../../../shared-ui/src/lib/data/management-data-client';
import { UserTeam } from '../../../../shared-ui/src/cloud-api/user-team';
import { CarConfiguration } from '../../../../shared-ui/src/models/car-configuration';
import { CarConfigurationSummary } from '../../../../shared-ui/src/models/car-configuration-summary';
import { ChannelDefinition } from '../../../../shared-ui/src/models/channel-definition';
import { DiscoveredRacecar } from '../../../../shared-ui/src/models/discovered-racecar';
import { TeamSelectionService } from '../teams/team-selection.service';

@Injectable()
export class LocalManagementDataClient implements ManagementDataClient {
  constructor(
    private readonly httpClient: HttpClient,
    @Inject(MANAGEMENT_DATA_CLIENT_SETTINGS)
    private readonly settings: ManagementDataClientSettings,
    private readonly injector: Injector
  ) {}

  async listDiscoveredRacecarsAsync(): Promise<DiscoveredRacecar[]> {
    return await firstValueFrom(
      this.httpClient.get<DiscoveredRacecar[]>(this.buildUrl('v1.0/discovery/list-racecars'))
    );
  }

  async getActiveRacecarAsync(): Promise<DiscoveredRacecar | null> {
    const response = await firstValueFrom(
      this.httpClient.get<DiscoveredRacecar>(this.buildUrl('v1.0/discovery/get-active-racecar'), {
        observe: 'response'
      })
    );
    return response.status === 204 ? null : response.body;
  }

  async selectRacecarAsync(name: string): Promise<DiscoveredRacecar> {
    return await firstValueFrom(
      this.httpClient.post<DiscoveredRacecar>(
        this.buildUrl('v1.0/discovery/select-racecar'),
        null,
        { params: new HttpParams().set('name', name) }
      )
    );
  }

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

  async loadAvailableUnitTypesAsync(dataType?: string): Promise<string[]> {
    let params = new HttpParams();
    if (dataType && dataType.trim().length > 0) {
      params = params.set('dataType', dataType);
    }

    return await firstValueFrom(
      this.httpClient.get<string[]>(
        this.buildActionUrl('load-available-unit-types'),
        { params }
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
    const teamId = this.injector.get(TeamSelectionService).selectedTeamId();
    let params = new HttpParams();
    if (teamId !== null) {
      params = params.set('teamId', String(teamId));
    }
    return await firstValueFrom(
        this.httpClient.post<CarConfiguration>(
          this.buildActionUrl('save-car-configuration'),
          carConfiguration,
          { params }
        )
    );
  }

  async listMyTeamsAsync(): Promise<UserTeam[]> {
    return await firstValueFrom(
      this.httpClient.get<UserTeam[]>(this.buildActionUrl('list-my-teams'))
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
    return this.buildUrl(`v1.0/data/${action}`);
  }

  private buildUrl(path: string): string {
    const trimmedBaseUrl = this.settings.baseServerUrl.replace(/\/$/, '');
    return `${trimmedBaseUrl}/${path}`;
  }
}

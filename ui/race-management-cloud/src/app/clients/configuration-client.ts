import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { Car } from '../../../../shared-ui/src/cloud-api/car';
import { CarRequest } from '../../../../shared-ui/src/cloud-api/car-request';
import { CarUpdate } from '../../../../shared-ui/src/cloud-api/car-update';
import { ChannelStatusTableConfiguration } from '../../../../shared-ui/src/cloud-api/channel-status-table-configuration';
import { Race } from '../../../../shared-ui/src/cloud-api/race';
import { SaveCarConfigurationResult } from '../../../../shared-ui/src/cloud-api/save-car-configuration-result';
import { SiteSettings } from '../../../../shared-ui/src/cloud-api/site-settings';
import { Team } from '../../../../shared-ui/src/cloud-api/team';
import { TeamRequest } from '../../../../shared-ui/src/cloud-api/team-request';
import { UserTeam } from '../../../../shared-ui/src/cloud-api/user-team';
import { CarConfiguration } from '../../../../shared-ui/src/models/car-configuration';

const API_PREFIX = 'v1';
const CONTROLLER = 'configuration';

@Injectable({ providedIn: 'root' })
export class ConfigurationClient {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  loadCarConfigurationByCar(teamId: number, carNumber: string): Promise<CarConfiguration> {
    return firstValueFrom(
      this.http.get<CarConfiguration>(this.url('load-car-configuration-by-car'), {
        params: { teamId, carNumber },
      }),
    );
  }

  loadCarConfigurationById(teamId: number, configurationId: string): Promise<CarConfiguration> {
    return firstValueFrom(
      this.http.get<CarConfiguration>(this.url('load-car-configuration-by-id'), {
        params: { teamId, configurationId },
      }),
    );
  }

  saveCarConfiguration(teamId: number, configuration: CarConfiguration): Promise<SaveCarConfigurationResult> {
    return firstValueFrom(
      this.http.post<SaveCarConfigurationResult>(this.url('save-car-configuration'), configuration, {
        params: { teamId },
      }),
    );
  }

  deleteCarConfiguration(teamId: number, configurationId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(this.url('delete-car-configuration'), {
        params: { teamId, configurationId },
      }),
    );
  }

  listMyTeams(): Promise<UserTeam[]> {
    return firstValueFrom(
      this.http.get<UserTeam[]>(this.url('list-my-teams')),
    );
  }

  loadTeams(): Promise<Team[]> {
    return firstValueFrom(
      this.http.get<Team[]>(this.url('load-teams')),
    );
  }

  createTeam(request: TeamRequest): Promise<Team> {
    return firstValueFrom(
      this.http.post<Team>(this.url('create-team'), request),
    );
  }

  getTeam(teamId: number): Promise<Team> {
    return firstValueFrom(
      this.http.get<Team>(this.url('get-team'), { params: { teamId } }),
    );
  }

  updateTeam(teamId: number, request: TeamRequest): Promise<Team> {
    return firstValueFrom(
      this.http.post<Team>(this.url('update-team'), request, {
        params: { teamId },
      }),
    );
  }

  deleteTeam(teamId: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(this.url('delete-team'), { params: { teamId } }),
    );
  }

  createCar(teamId: number, request: CarRequest): Promise<Car> {
    return firstValueFrom(
      this.http.post<Car>(this.url('create-car'), request, {
        params: { teamId },
      }),
    );
  }

  getCar(teamId: number, carNumber: string): Promise<Car> {
    return firstValueFrom(
      this.http.get<Car>(this.url('get-car'), {
        params: { teamId, carNumber },
      }),
    );
  }

  listCars(teamId: number): Promise<Car[]> {
    return firstValueFrom(
      this.http.get<Car[]>(this.url('list-cars'), { params: { teamId } }),
    );
  }

  updateCar(teamId: number, carNumber: string, request: CarUpdate): Promise<Car> {
    return firstValueFrom(
      this.http.post<Car>(this.url('update-car'), request, {
        params: { teamId, carNumber },
      }),
    );
  }

  deleteCar(teamId: number, carNumber: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(this.url('delete-car'), {
        params: { teamId, carNumber },
      }),
    );
  }

  loadRace(teamId: number, raceId: number): Promise<Race> {
    return firstValueFrom(
      this.http.get<Race>(this.url('load-race'), {
        params: { teamId, raceId },
      }),
    );
  }

  listRaces(teamId: number): Promise<Race[]> {
    return firstValueFrom(
      this.http.get<Race[]>(this.url('list-races'), { params: { teamId } }),
    );
  }

  saveRace(teamId: number, race: Race): Promise<Race> {
    return firstValueFrom(
      this.http.post<Race>(this.url('save-race'), race, {
        params: { teamId },
      }),
    );
  }

  deleteRace(teamId: number, raceId: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(this.url('delete-race'), {
        params: { teamId, raceId },
      }),
    );
  }

  async loadSiteSettings(teamId: number): Promise<SiteSettings | null> {
    try {
      return await firstValueFrom(
        this.http.get<SiteSettings>(this.url('load-site-settings'), {
          params: { teamId },
        }),
      );
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) return null;
      throw err;
    }
  }

  saveSiteSettings(teamId: number, settings: SiteSettings): Promise<SiteSettings> {
    return firstValueFrom(
      this.http.post<SiteSettings>(this.url('save-site-settings'), settings, {
        params: { teamId },
      }),
    );
  }

  async loadChannelStatusTableConfiguration(
    teamId: number,
    userId: string,
  ): Promise<ChannelStatusTableConfiguration | null> {
    try {
      return await firstValueFrom(
        this.http.get<ChannelStatusTableConfiguration>(this.url('load-channel-status-table-configuration'), {
          params: { teamId, userId },
        }),
      );
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) return null;
      throw err;
    }
  }

  saveChannelStatusTableConfiguration(
    teamId: number,
    userId: string,
    configuration: ChannelStatusTableConfiguration,
  ): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(this.url('save-channel-status-table-configuration'), configuration, {
        params: { teamId, userId },
      }),
    );
  }

  private url(action: string): string {
    const base = this.config.api.baseUrl.replace(/\/$/, '');
    return `${base}/${API_PREFIX}/${CONTROLLER}/${action}`;
  }
}

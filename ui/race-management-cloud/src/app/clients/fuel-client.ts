import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { CalibrationFactor } from '../../../../shared-ui/src/cloud-api/calibration-factor';
import { CalibrationOverrideRequest } from '../../../../shared-ui/src/cloud-api/calibration-override-request';
import { CarFuelConfig } from '../../../../shared-ui/src/models/car-fuel-config';
import { CreateStintRequest } from '../../../../shared-ui/src/cloud-api/create-stint-request';
import { EnterVolumeRequest } from '../../../../shared-ui/src/cloud-api/enter-volume-request';
import { FuelRangeSnapshot } from '../../../../shared-ui/src/cloud-api/fuel-range-snapshot';
import { FuelWindow } from '../../../../shared-ui/src/cloud-api/fuel-window';
import { ManualRefuelRequest } from '../../../../shared-ui/src/cloud-api/manual-refuel-request';
import { RefuelEvent } from '../../../../shared-ui/src/cloud-api/refuel-event';
import { RefuelEventPublishResult } from '../../../../shared-ui/src/cloud-api/refuel-event-publish-result';
import { Stint } from '../../../../shared-ui/src/cloud-api/stint';
import { UpdateStintRequest } from '../../../../shared-ui/src/cloud-api/update-stint-request';

const API_PREFIX = 'v1';
const CONTROLLER = 'fuel';

@Injectable({ providedIn: 'root' })
export class FuelClient {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  async loadFuelSnapshot(teamId: number, carNumber: string, raceId: number): Promise<FuelRangeSnapshot | null> {
    try {
      return await firstValueFrom(
        this.http.get<FuelRangeSnapshot>(this.url('load-fuel-snapshot'), {
          params: { teamId, carNumber, raceId },
        }),
      );
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) return null;
      throw err;
    }
  }

  saveManualRefuel(
    teamId: number,
    carNumber: string,
    request: ManualRefuelRequest,
  ): Promise<RefuelEventPublishResult> {
    return firstValueFrom(
      this.http.post<RefuelEventPublishResult>(this.url('save-manual-refuel'), request, {
        params: { teamId, carNumber },
      }),
    );
  }

  enterRefuelVolume(
    teamId: number,
    refuelEventId: number,
    request: EnterVolumeRequest,
  ): Promise<RefuelEventPublishResult> {
    return firstValueFrom(
      this.http.post<RefuelEventPublishResult>(this.url('enter-refuel-volume'), request, {
        params: { teamId, refuelEventId },
      }),
    );
  }

  async loadCalibrationFactor(teamId: number, carNumber: string): Promise<CalibrationFactor | null> {
    try {
      return await firstValueFrom(
        this.http.get<CalibrationFactor>(this.url('load-calibration-factor'), {
          params: { teamId, carNumber },
        }),
      );
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) return null;
      throw err;
    }
  }

  saveCalibrationOverride(
    teamId: number,
    carNumber: string,
    request: CalibrationOverrideRequest,
  ): Promise<CalibrationFactor> {
    return firstValueFrom(
      this.http.post<CalibrationFactor>(this.url('save-calibration-override'), request, {
        params: { teamId, carNumber },
      }),
    );
  }

  resetCalibration(teamId: number, carNumber: string): Promise<CalibrationFactor> {
    return firstValueFrom(
      this.http.post<CalibrationFactor>(this.url('reset-calibration'), null, {
        params: { teamId, carNumber },
      }),
    );
  }

  resumeCalibrationLearning(teamId: number, carNumber: string): Promise<CalibrationFactor> {
    return firstValueFrom(
      this.http.post<CalibrationFactor>(this.url('resume-calibration-learning'), null, {
        params: { teamId, carNumber },
      }),
    );
  }

  loadRefuelEvents(teamId: number, carNumber: string, raceId: number): Promise<RefuelEvent[]> {
    return firstValueFrom(
      this.http.get<RefuelEvent[]>(this.url('load-refuel-events'), {
        params: { teamId, carNumber, raceId },
      }),
    );
  }

  loadFuelWindows(teamId: number, carNumber: string, raceId: number): Promise<FuelWindow[]> {
    return firstValueFrom(
      this.http.get<FuelWindow[]>(this.url('load-fuel-windows'), {
        params: { teamId, carNumber, raceId },
      }),
    );
  }

  loadStints(teamId: number, carNumber: string, raceId: number): Promise<Stint[]> {
    return firstValueFrom(
      this.http.get<Stint[]>(this.url('load-stints'), {
        params: { teamId, carNumber, raceId },
      }),
    );
  }

  async loadCarFuelConfig(teamId: number, carNumber: string): Promise<CarFuelConfig | null> {
    try {
      return await firstValueFrom(
        this.http.get<CarFuelConfig>(this.url('load-car-fuel-config'), {
          params: { teamId, carNumber },
        }),
      );
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) return null;
      throw err;
    }
  }

  saveStint(teamId: number, request: CreateStintRequest): Promise<Stint> {
    return firstValueFrom(
      this.http.post<Stint>(this.url('save-stint'), request, { params: { teamId } }),
    );
  }

  updateStint(teamId: number, stintId: number, request: UpdateStintRequest): Promise<Stint> {
    return firstValueFrom(
      this.http.post<Stint>(this.url('update-stint'), request, { params: { teamId, stintId } }),
    );
  }

  deleteStint(teamId: number, stintId: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(this.url('delete-stint'), null, { params: { teamId, stintId } }),
    );
  }

  private url(action: string): string {
    const base = this.config.api.baseUrl.replace(/\/$/, '');
    return `${base}/${API_PREFIX}/${CONTROLLER}/${action}`;
  }
}

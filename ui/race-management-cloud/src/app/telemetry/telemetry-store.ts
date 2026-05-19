import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { CarChannelSnapshot } from '../../../../shared-ui/src/cloud-api/car-channel-snapshot';
import { ChannelValueSnapshot } from '../../../../shared-ui/src/cloud-api/channel-value-snapshot';
import { AuthService } from '../auth.service';
import { HubClient, HubConnectionStatus } from '../clients/hub-client';
import { TeamSelectionService } from '../teams/team-selection.service';

@Injectable({ providedIn: 'root' })
export class TelemetryStore {
  private readonly hub = inject(HubClient);
  private readonly auth = inject(AuthService);
  private readonly teamSelection = inject(TeamSelectionService);

  private readonly _carsByKey = signal<ReadonlyMap<string, CarChannelSnapshot>>(new Map());
  private readonly _connectionStatus = signal<HubConnectionStatus>('Disconnected');

  readonly carsByKey = computed(() => this._carsByKey());
  readonly connectionStatus = computed(() => this._connectionStatus());

  constructor() {
    this.hub.connectionStatus$.subscribe(s => {
      this._connectionStatus.set(s);
      if (s === 'Connected') void this.subscribeIfNeeded();
    });
    this.hub.channelSnapshot$.subscribe(cars => this.applySnapshot(cars));
    this.hub.channelValueChanged$.subscribe(({ carKey, change }) => {
      const existing = this._carsByKey().get(carKey);
      if (!existing) return;
      const channels = { ...this.channelsAsObject(existing.channels) };
      channels[change.sessionIndex] = { value: change.value, timestamp: change.timestamp };
      const next = new Map(this._carsByKey());
      next.set(carKey, { ...existing, channels });
      this._carsByKey.set(next);
    });

    effect(() => {
      if (this.auth.isLoggedIn()) {
        void this.hub.connect();
      } else {
        void this.hub.disconnect();
        this._carsByKey.set(new Map());
      }
    });

    // Re-subscribe whenever the selected team changes; clears the cars map so
    // we don't show stale data from the previous team while the snapshot lands.
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) return;
      this._carsByKey.set(new Map());
      void this.subscribeIfNeeded();
    });
  }

  private async subscribeIfNeeded(): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) return;
    if (this._connectionStatus() !== 'Connected') return;
    try {
      await this.hub.subscribeToTeam(teamId);
    } catch (err) {
      console.error('Failed to subscribe to team', teamId, err);
    }
  }

  /** Find the snapshot for a car by its number (not by carKey). */
  carForNumber(carNumber: string): CarChannelSnapshot | undefined {
    for (const car of this._carsByKey().values()) {
      if (car.carNumber === carNumber) return car;
    }
    return undefined;
  }

  /** Newest channel timestamp on the car, or null if no channels reported yet. */
  lastTelemetryFor(carNumber: string): Date | null {
    const car = this.carForNumber(carNumber);
    if (!car) return null;
    let latestMs = 0;
    for (const ch of Object.values(this.channelsAsObject(car.channels))) {
      const t = ch.timestamp.getTime();
      if (Number.isFinite(t) && t > latestMs) latestMs = t;
    }
    return latestMs === 0 ? null : new Date(latestMs);
  }

  private applySnapshot(cars: CarChannelSnapshot[]): void {
    const next = new Map(this._carsByKey());
    for (const car of cars) {
      next.set(car.carKey, { ...car, channels: this.channelsAsObject(car.channels) });
    }
    this._carsByKey.set(next);
  }

  /**
   * MessagePack may deliver the channels dictionary as either a plain object or a Map
   * depending on key types. Normalize to a plain object keyed by sessionIndex.
   */
  private channelsAsObject(channels: unknown): { [key: number]: ChannelValueSnapshot } {
    if (channels instanceof Map) {
      const result: { [key: number]: ChannelValueSnapshot } = {};
      for (const [k, v] of channels) {
        result[Number(k)] = v as ChannelValueSnapshot;
      }
      return result;
    }
    return (channels ?? {}) as { [key: number]: ChannelValueSnapshot };
  }
}

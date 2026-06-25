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
  private readonly _connectedCarKeys = signal<ReadonlySet<string>>(new Set());
  // carKey -> epoch ms when CarHub reported the car disconnected. Lets the UI show a
  // brief "recently disconnected" (yellow) grace state before settling to red.
  private readonly _disconnectedAt = signal<ReadonlyMap<string, number>>(new Map());

  readonly carsByKey = computed(() => this._carsByKey());
  readonly connectionStatus = computed(() => this._connectionStatus());
  readonly connectedCarKeys = computed(() => this._connectedCarKeys());

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
    this.hub.carConnectionSnapshot$.subscribe(carKeys => {
      this._connectedCarKeys.set(new Set(carKeys));
      this._disconnectedAt.set(new Map());
    });
    this.hub.carConnectionChanged$.subscribe(change => {
      const next = new Set(this._connectedCarKeys());
      const disconnectedAt = new Map(this._disconnectedAt());
      if (change.isConnected) {
        next.add(change.carKey);
        disconnectedAt.delete(change.carKey);
      } else {
        next.delete(change.carKey);
        disconnectedAt.set(change.carKey, Date.now());
      }
      this._connectedCarKeys.set(next);
      this._disconnectedAt.set(disconnectedAt);
    });

    effect(() => {
      if (this.auth.isLoggedIn()) {
        void this.hub.connect();
      } else {
        void this.hub.disconnect();
        this._carsByKey.set(new Map());
        this._connectedCarKeys.set(new Set());
        this._disconnectedAt.set(new Map());
      }
    });

    // Re-subscribe whenever the selected team changes; clears the cars map so
    // we don't show stale data from the previous team while the snapshot lands.
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) return;
      this._carsByKey.set(new Map());
      this._connectedCarKeys.set(new Set());
      this._disconnectedAt.set(new Map());
      void this.subscribeIfNeeded();
    });
  }

  private carKeyFor(carNumber: string): string | null {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) return null;
    return `team-${teamId}-car-${carNumber}`;
  }

  /** True if the car is currently connected to CarHub for the selected team. */
  isCarConnected(carNumber: string): boolean {
    const carKey = this.carKeyFor(carNumber);
    return carKey !== null && this._connectedCarKeys().has(carKey);
  }

  /**
   * Epoch ms of the most recent CarHub disconnect for this car, or null if it has not
   * disconnected since the last snapshot. Used to render the brief yellow grace state.
   */
  disconnectedAtFor(carNumber: string): number | null {
    const carKey = this.carKeyFor(carNumber);
    if (carKey === null) return null;
    return this._disconnectedAt().get(carKey) ?? null;
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

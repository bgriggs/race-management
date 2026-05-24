import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { Race } from '../../../../shared-ui/src/cloud-api/race';
import { ConfigurationClient } from '../clients/configuration-client';
import { TeamSelectionService } from '../teams/team-selection.service';

const ONE_DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Shared race selection + 1Hz "now" clock for the Race Monitor.
 *
 * Owned at root so both the RaceHeader (which presents the picker) and the
 * Fuel & Pit Strategy Gantt (which positions stint bars and the "now" marker
 * on the race timeline) read from a single source of truth and a single
 * setInterval, rather than each component spinning its own.
 */
@Injectable({ providedIn: 'root' })
export class RaceSelectionService {
  private readonly client = inject(ConfigurationClient);
  private readonly teamSelection = inject(TeamSelectionService);

  private readonly _now = signal(new Date());
  private readonly _races = signal<Race[]>([]);
  private readonly _selectedRaceId = signal<number | null>(null);

  readonly now = computed(() => this._now());
  readonly races = computed(() => this._races());
  readonly selectedRaceId = computed(() => this._selectedRaceId());
  readonly selectedRace = computed(() => {
    const id = this._selectedRaceId();
    return id === null ? null : this._races().find(r => r.id === id) ?? null;
  });

  /**
   * The race whose [start, start+duration) brackets `now`. Drives session-
   * dependent UI (Fuel & Pit Strategy, Competitor Analysis) per CONTEXT.md.
   */
  readonly activeRace = computed(() => {
    const now = this._now().getTime();
    return this._races().find(r => {
      const start = new Date(r.start).getTime();
      const end = start + r.duration * 60 * 60 * 1000;
      return start <= now && now < end;
    }) ?? null;
  });

  constructor() {
    setInterval(() => this._now.set(new Date()), 1000);

    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this._races.set([]);
        this._selectedRaceId.set(null);
        return;
      }
      void this.loadRaces(teamId);
    });
  }

  selectRace(raceId: number): void {
    this._selectedRaceId.set(raceId);
  }

  async refresh(): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId !== null) await this.loadRaces(teamId);
  }

  private async loadRaces(teamId: number): Promise<void> {
    try {
      const all = await this.client.listRaces(teamId);
      const cutoff = Date.now() + ONE_DAY_MS;
      const filtered = all
        .filter(r => new Date(r.start).getTime() <= cutoff)
        .sort((a, b) => new Date(b.start).getTime() - new Date(a.start).getTime());
      this._races.set(filtered);
      const current = this._selectedRaceId();
      if (current === null && filtered.length > 0) {
        this._selectedRaceId.set(filtered[0].id);
      } else if (current !== null && !filtered.some(r => r.id === current)) {
        this._selectedRaceId.set(filtered.length > 0 ? filtered[0].id : null);
      }
    } catch (err) {
      console.error('Failed to load races:', err);
      this._races.set([]);
    }
  }
}

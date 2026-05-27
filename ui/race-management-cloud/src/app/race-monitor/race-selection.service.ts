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
    // Optimistic local update so the dropdown reflects the choice instantly,
    // then persist to the team so ChannelProcessor re-subscribes to this race's
    // event. The WebApi handler publishes a pub/sub notification that wakes the
    // worker — no need to wait for the 30s poll tick.
    this._selectedRaceId.set(raceId);
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) return;
    this.client.selectRace(teamId, raceId).catch(err => {
      console.error('Failed to persist race selection:', err);
    });
  }

  async refresh(): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId !== null) await this.loadRaces(teamId);
  }

  private async loadRaces(teamId: number): Promise<void> {
    try {
      // Load races and the team's persisted selection in parallel. The selection is the
      // shared per-team "which race is being monitored" — drives the ChannelProcessor.
      // If unset, fall back to the most recent race so the dropdown isn't empty.
      const [all, team] = await Promise.all([
        this.client.listRaces(teamId),
        this.client.getTeam(teamId).catch(() => null),
      ]);
      const cutoff = Date.now() + ONE_DAY_MS;
      const filtered = all
        .filter(r => new Date(r.start).getTime() <= cutoff)
        .sort((a, b) => new Date(b.start).getTime() - new Date(a.start).getTime());
      this._races.set(filtered);

      const persisted = team?.selectedRaceId ?? null;
      if (persisted !== null && filtered.some(r => r.id === persisted)) {
        this._selectedRaceId.set(persisted);
      } else if (filtered.length > 0) {
        this._selectedRaceId.set(filtered[0].id);
      } else {
        this._selectedRaceId.set(null);
      }
    } catch (err) {
      console.error('Failed to load races:', err);
      this._races.set([]);
    }
  }
}

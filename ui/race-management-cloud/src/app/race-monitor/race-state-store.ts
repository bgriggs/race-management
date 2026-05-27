import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { RaceStateDto } from '../../../../shared-ui/src/cloud-api/race-state-dto';
import { HubClient } from '../clients/hub-client';
import { TeamSelectionService } from '../teams/team-selection.service';

/**
 * Latest RedMist-sourced race-header state, pushed over WebHub. The store exposes a
 * single `state` signal that is `null` when RedMist isn't streaming for the current
 * team (no event paired, lease lost, creds missing, etc.). The race-header reads this
 * directly and renders blanks on null — no client-side fallback.
 *
 * State is cleared on team change to avoid showing the previous team's values while
 * the WebHub subscribe-snapshot lands.
 */
@Injectable({ providedIn: 'root' })
export class RaceStateStore {
  private readonly hub = inject(HubClient);
  private readonly teamSelection = inject(TeamSelectionService);

  private readonly _state = signal<RaceStateDto | null>(null);
  readonly state = computed(() => this._state());

  constructor() {
    this.hub.raceStateChanged$.subscribe(s => this._state.set(s));

    // Clear on team change so the header doesn't briefly show another team's race time
    // while the WebHub subscribe handshake pushes a fresh snapshot.
    effect(() => {
      this.teamSelection.selectedTeamId();
      this._state.set(null);
    });
  }
}

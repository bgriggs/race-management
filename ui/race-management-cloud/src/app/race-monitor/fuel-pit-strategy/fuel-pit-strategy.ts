import { Component, computed, effect, inject, signal } from '@angular/core';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { RaceSelectionService } from '../race-selection.service';
import { FuelRow } from './fuel-row/fuel-row';

@Component({
  selector: 'app-fuel-pit-strategy',
  imports: [FuelRow],
  templateUrl: './fuel-pit-strategy.html',
  styleUrl: './fuel-pit-strategy.css',
})
export class FuelPitStrategy {
  private readonly client = inject(ConfigurationClient);
  protected readonly teamSelection = inject(TeamSelectionService);
  protected readonly raceSelection = inject(RaceSelectionService);

  protected readonly cars = signal<Car[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly activeRace = this.raceSelection.activeRace;
  protected readonly hasActiveRace = computed(() => this.activeRace() !== null);

  /**
   * Diagnostic surfaced under the "No active race session" placeholder so the
   * engineer can see why the clock-driven gate is failing — typically because
   * the race's start is in the future or its end is in the past. Lines stay
   * empty when no race is selected.
   */
  protected readonly noActiveRaceDiagnostic = computed(() => {
    const selected = this.raceSelection.selectedRace();
    const now = this.raceSelection.now();
    if (!selected) return null;
    const start = new Date(selected.start);
    const end = new Date(start.getTime() + selected.duration * 60 * 60 * 1000);
    const reason = now < start
      ? 'Race has not started yet.'
      : now >= end
        ? 'Race has already ended.'
        : 'Race brackets now — should be active. Refresh races?';
    return {
      name: selected.name,
      start: start.toLocaleString(),
      end: end.toLocaleString(),
      now: now.toLocaleString(),
      reason,
    };
  });

  constructor() {
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this.cars.set([]);
        return;
      }
      void this.loadCars(teamId);
    });
  }

  private async loadCars(teamId: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.cars.set(await this.client.listCars(teamId));
    } catch (err) {
      console.error('Failed to load cars for fuel-pit-strategy:', err);
      this.error.set('Failed to load cars.');
    } finally {
      this.loading.set(false);
    }
  }
}

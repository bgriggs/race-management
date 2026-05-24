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

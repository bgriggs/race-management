import { Component, computed, effect, inject, signal } from '@angular/core';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { RaceSelectionService } from '../race-selection.service';
import { FuelCarRow } from './fuel-car-row/fuel-car-row';
import { ConfidenceTier, FuelConfidenceToggle } from './fuel-confidence-toggle/fuel-confidence-toggle';
import { FuelDetailPanel } from './fuel-detail-panel/fuel-detail-panel';
import { FuelScopeToggle, GanttScope } from './fuel-scope-toggle/fuel-scope-toggle';
import { GanttContainer } from './gantt-container/gantt-container';

@Component({
  selector: 'app-fuel-pit-strategy',
  imports: [FuelCarRow, FuelConfidenceToggle, FuelScopeToggle, FuelDetailPanel, GanttContainer],
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

  /** Session-only view state. Resets on reload (spec §3, §4). */
  protected readonly confidenceTier = signal<ConfidenceTier>('regular');
  protected readonly scope = signal<GanttScope>('current');

  /**
   * Per-spec §7, the panel holds at-most-one selected car. The detail modal
   * is rendered once at panel level (rather than per-row) so selection
   * state is the single source of truth.
   */
  protected readonly selectedCarNumber = signal<string | null>(null);
  protected readonly selectedCar = computed(() =>
    this.cars().find(c => c.number === this.selectedCarNumber()) ?? null,
  );

  /**
   * Latest canvas content width emitted by each `<app-fuel-car-row>`. The gantt-container
   * passes the max into the shared time-axis so its scrollable width matches the row's
   * scope-aware extent (no time-axis overhang past the row's content).
   */
  protected readonly rowCanvasWidths = signal<Map<string, number>>(new Map());
  protected readonly maxCanvasWidthPx = computed(() => {
    let max = 0;
    for (const w of this.rowCanvasWidths().values()) if (w > max) max = w;
    return max > 0 ? max : null;
  });

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

  /**
   * Toggle behavior per spec §7: clicking the currently-selected row deselects;
   * any other click selects.
   */
  protected onRowClick(carNumber: string): void {
    this.selectedCarNumber.update(current => current === carNumber ? null : carNumber);
  }

  /**
   * Settings always opens — never closes — per spec §19.
   */
  protected onSettingsClick(carNumber: string): void {
    this.selectedCarNumber.set(carNumber);
  }

  /**
   * Add Stint button. Phase 5 wires this to the fuel-add-stint modal; for
   * now the click is captured so the row-level handler runs but no UI opens.
   */
  protected onAddStintClick(carNumber: string): void {
    void carNumber;
  }

  /** Closing the detail modal clears selection (spec §7). */
  protected onDetailClosed(): void {
    this.selectedCarNumber.set(null);
  }

  protected onCanvasWidthPxChange(carNumber: string, widthPx: number): void {
    const next = new Map(this.rowCanvasWidths());
    next.set(carNumber, widthPx);
    this.rowCanvasWidths.set(next);
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

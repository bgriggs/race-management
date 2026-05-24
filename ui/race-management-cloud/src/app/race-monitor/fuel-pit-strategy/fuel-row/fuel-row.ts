import { Component, computed, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { FuelRangeSnapshot } from '../../../../../../shared-ui/src/cloud-api/fuel-range-snapshot';
import { FuelWindow } from '../../../../../../shared-ui/src/cloud-api/fuel-window';
import { Race } from '../../../../../../shared-ui/src/cloud-api/race';
import { RefuelEvent } from '../../../../../../shared-ui/src/cloud-api/refuel-event';
import { Stint } from '../../../../../../shared-ui/src/cloud-api/stint';
import { FuelClient } from '../../../clients/fuel-client';
import { RaceSelectionService } from '../../race-selection.service';
import { AddFuelDialog, AddFuelDialogMode } from '../add-fuel-dialog/add-fuel-dialog';
import { FuelDetailPanel } from '../fuel-detail-panel/fuel-detail-panel';
import { GanttBar, StintBoundary } from '../gantt-bar/gantt-bar';

const POLL_INTERVAL_MS = 5000;

interface RenderedStint {
  stint: Stint;
  xPct: number;
  widthPct: number;
  boundary: StintBoundary;
  fuelWindow: FuelWindow | null;
  startRefuel: RefuelEvent | null;
  label: string;
}

@Component({
  selector: 'app-fuel-row',
  imports: [GanttBar, AddFuelDialog, FuelDetailPanel],
  templateUrl: './fuel-row.html',
  styleUrl: './fuel-row.css',
})
export class FuelRow {
  private readonly fuel = inject(FuelClient);
  private readonly raceSelection = inject(RaceSelectionService);

  readonly teamId = input.required<number>();
  readonly car = input.required<Car>();
  readonly race = input.required<Race>();

  private readonly snapshot = signal<FuelRangeSnapshot | null>(null);
  private readonly refuelEvents = signal<RefuelEvent[]>([]);
  private readonly fuelWindows = signal<FuelWindow[]>([]);
  private readonly stints = signal<Stint[]>([]);

  protected readonly dialogMode = signal<AddFuelDialogMode | null>(null);
  protected readonly detailOpen = signal(false);
  private readonly dismissingIds = signal<ReadonlySet<number>>(new Set());

  protected readonly nowPct = computed(() => {
    const race = this.race();
    const start = new Date(race.start).getTime();
    const span = race.duration * 60 * 60 * 1000;
    const now = this.raceSelection.now().getTime();
    return clamp(((now - start) / span) * 100, 0, 100);
  });

  protected readonly renderedStints = computed<RenderedStint[]>(() => {
    const race = this.race();
    const start = new Date(race.start).getTime();
    const span = race.duration * 60 * 60 * 1000;
    const now = this.raceSelection.now().getTime();

    const stints = [...this.stints()].sort(
      (a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime(),
    );

    const windowsById = new Map(this.fuelWindows().map(w => [w.id, w]));
    const refuelsById = new Map(this.refuelEvents().map(r => [r.id, r]));

    return stints.map((stint, index): RenderedStint => {
      const stintStart = new Date(stint.startAt).getTime();
      const stintEnd = stint.endAt ? new Date(stint.endAt).getTime() : now;
      const xPct = clamp(((stintStart - start) / span) * 100, 0, 100);
      const endPct = clamp(((stintEnd - start) / span) * 100, 0, 100);
      const widthPct = Math.max(0.25, endPct - xPct);

      const window = windowsById.get(stint.fuelWindowId) ?? null;
      const startRefuel = window ? refuelsById.get(window.startRefuelEventId) ?? null : null;

      const prev = index > 0 ? stints[index - 1] : null;
      const boundary: StintBoundary = prev === null
        ? 'first'
        : prev.fuelWindowId === stint.fuelWindowId
          ? 'non-fuel-pit'
          : 'new-fuel-window';

      return {
        stint,
        xPct,
        widthPct,
        boundary,
        fuelWindow: window,
        startRefuel,
        label: buildStintLabel(stint, window, startRefuel),
      };
    });
  });

  /**
   * Pending RefuelEvents for this car — auto-detected events the engineer
   * has not yet entered a volume for. Drives the per-row banner. Excludes
   * events currently being dismissed (optimistic hide).
   */
  protected readonly pendingRefuels = computed<RefuelEvent[]>(() => {
    const dismissing = this.dismissingIds();
    return this.refuelEvents()
      .filter(e => e.status === 'Pending' && !dismissing.has(e.id))
      .sort((a, b) => new Date(a.detectedAt).getTime() - new Date(b.detectedAt).getTime());
  });

  protected readonly primaryMinutes = computed(() => {
    const r = this.snapshot()?.primary.rangeMinutes;
    return r == null ? '—' : `${Math.round(r)} min`;
  });
  protected readonly primaryGallons = computed(() => {
    const r = this.snapshot()?.primary.rangeGallons;
    return r == null ? '—' : `${r.toFixed(1)} gal`;
  });
  protected readonly highConfMinutes = computed(() => {
    const r = this.snapshot()?.highConfidence.rangeMinutes;
    return r == null ? '—' : `${Math.round(r)} min`;
  });
  protected readonly primaryConfidencePct = computed(() => {
    const c = this.snapshot()?.primary.confidence;
    return c == null ? null : Math.round(c * 100);
  });

  constructor() {
    const destroyRef = inject(DestroyRef);
    let interval: ReturnType<typeof setInterval> | null = null;

    effect(() => {
      const teamId = this.teamId();
      const carNumber = this.car().number;
      const raceId = this.race().id;

      if (interval !== null) {
        clearInterval(interval);
        interval = null;
      }

      void this.refreshAll(teamId, carNumber, raceId);
      interval = setInterval(() => {
        void this.refreshAll(teamId, carNumber, raceId);
      }, POLL_INTERVAL_MS);
    });

    destroyRef.onDestroy(() => {
      if (interval !== null) clearInterval(interval);
    });
  }

  protected openManualDialog(): void {
    this.dialogMode.set({ kind: 'manual' });
  }

  protected openEnterVolumeDialog(event: RefuelEvent): void {
    this.dialogMode.set({
      kind: 'enter-volume',
      refuelEventId: event.id,
      defaultAt: new Date(event.detectedAt),
    });
  }

  protected closeDialog(): void {
    this.dialogMode.set(null);
  }

  protected openDetail(): void {
    this.detailOpen.set(true);
  }

  protected closeDetail(): void {
    this.detailOpen.set(false);
  }

  protected async onDialogSaved(): Promise<void> {
    this.dialogMode.set(null);
    await this.refreshAll(this.teamId(), this.car().number, this.race().id);
  }

  protected async dismissPending(event: RefuelEvent): Promise<void> {
    this.dismissingIds.update(s => new Set(s).add(event.id));
    try {
      await this.fuel.dismissRefuelEvent(this.teamId(), event.id);
      await this.refreshAll(this.teamId(), this.car().number, this.race().id);
    } catch (err) {
      console.error('Failed to dismiss refuel event', event.id, err);
    } finally {
      this.dismissingIds.update(s => {
        const next = new Set(s);
        next.delete(event.id);
        return next;
      });
    }
  }

  protected pendingLabel(event: RefuelEvent): string {
    const ts = new Date(event.detectedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    return `Refuel detected at ${ts}`;
  }

  private async refreshAll(teamId: number, carNumber: string, raceId: number): Promise<void> {
    try {
      const [snapshot, refuels, windows, stints] = await Promise.all([
        this.fuel.loadFuelSnapshot(teamId, carNumber),
        this.fuel.loadRefuelEvents(teamId, carNumber, raceId),
        this.fuel.loadFuelWindows(teamId, carNumber, raceId),
        this.fuel.loadStints(teamId, carNumber, raceId),
      ]);
      this.snapshot.set(snapshot);
      this.refuelEvents.set(refuels);
      this.fuelWindows.set(windows);
      this.stints.set(stints);
    } catch (err) {
      console.error('Failed to refresh fuel data for car', carNumber, err);
    }
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function buildStintLabel(stint: Stint, window: FuelWindow | null, startRefuel: RefuelEvent | null): string {
  const parts: string[] = [];
  parts.push(`Stint #${stint.id}`);
  const start = new Date(stint.startAt);
  parts.push(`start ${start.toLocaleTimeString()}`);
  if (stint.endAt) {
    parts.push(`end ${new Date(stint.endAt).toLocaleTimeString()}`);
  } else {
    parts.push('(open)');
  }
  if (window) {
    parts.push(`FuelWindow #${window.id}`);
    if (startRefuel?.enteredFuelGallons != null) {
      parts.push(`${startRefuel.enteredFuelGallons.toFixed(1)} gal entered`);
    }
  }
  return parts.join(' • ');
}

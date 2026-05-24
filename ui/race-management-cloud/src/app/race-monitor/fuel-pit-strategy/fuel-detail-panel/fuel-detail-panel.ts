import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { CalibrationFactor } from '../../../../../../shared-ui/src/cloud-api/calibration-factor';
import { CalibrationFactorSource } from '../../../../../../shared-ui/src/cloud-api/calibration-factor-source';
import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { EstimatorReadout } from '../../../../../../shared-ui/src/cloud-api/estimator-readout';
import { FuelRangeSnapshot } from '../../../../../../shared-ui/src/cloud-api/fuel-range-snapshot';
import { FuelWindow } from '../../../../../../shared-ui/src/cloud-api/fuel-window';
import { Race } from '../../../../../../shared-ui/src/cloud-api/race';
import { FuelClient } from '../../../clients/fuel-client';

const POLL_INTERVAL_MS = 5000;
const SPARKLINE_POINTS = 6;
const SPARKLINE_W = 120;
const SPARKLINE_H = 28;

interface EstimatorRow {
  readout: EstimatorReadout;
  displayName: string;
  rangeText: string;
  sigmaText: string;
  outlierBadge: string | null;
}

interface SparkPoint {
  x: number;
  y: number;
  value: number;
}

@Component({
  selector: 'app-fuel-detail-panel',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './fuel-detail-panel.html',
  styleUrl: './fuel-detail-panel.css',
})
export class FuelDetailPanel {
  private readonly fuel = inject(FuelClient);

  readonly teamId = input.required<number>();
  readonly car = input.required<Car>();
  readonly race = input.required<Race>();
  readonly closed = output<void>();

  protected readonly snapshot = signal<FuelRangeSnapshot | null>(null);
  protected readonly fuelWindows = signal<FuelWindow[]>([]);
  protected readonly calibration = signal<CalibrationFactor | null>(null);

  protected readonly overrideInput = signal('');
  protected readonly showOverrideInput = signal(false);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly primary = computed(() => this.snapshot()?.primary ?? null);
  protected readonly highConfidence = computed(() => this.snapshot()?.highConfidence ?? null);
  protected readonly reconciler = computed(() => this.snapshot()?.reconciler ?? null);
  protected readonly fuelWindowDetails = computed(() => this.snapshot()?.fuelWindow ?? null);

  protected readonly estimatorRows = computed<EstimatorRow[]>(() => {
    const snap = this.snapshot();
    if (!snap) return [];
    return snap.estimators.map(e => ({
      readout: e,
      displayName: displayNameFor(e.name),
      rangeText: e.available && e.rangeMinutes != null
        ? `${Math.round(e.rangeMinutes)} min · ${(e.rangeGallons ?? 0).toFixed(1)} gal`
        : '—',
      sigmaText: e.sigma != null ? `±${e.sigma.toFixed(2)} gal` : '',
      outlierBadge: e.isOutlier ? 'outlier' : null,
    }));
  });

  protected readonly rateBreakdown = computed(() => {
    const r = this.reconciler();
    if (!r || r.currentEffectiveRateGalPerMin == null) return null;
    const effective = r.currentEffectiveRateGalPerMin;
    const multipliers = r.paceMultiplier * r.flagMultiplier;
    const base = multipliers !== 0 ? effective / multipliers : null;
    return {
      base,
      flag: r.flagMultiplier,
      pace: r.paceMultiplier,
      effective,
    };
  });

  protected readonly calibrationLabel = computed(() => {
    const c = this.calibration();
    if (!c) return 'No calibration data yet';
    return `${c.value.toFixed(3)} (${sourceLabel(c.source)})`;
  });

  protected readonly isOverridden = computed(() =>
    this.calibration()?.source === CalibrationFactorSource.ManualOverride,
  );

  protected readonly sparklinePoints = computed<SparkPoint[]>(() => {
    const closed = this.fuelWindows()
      .filter(w => w.observedConsumptionGalPerMin != null && w.closedAt != null && !w.closedBySessionEnd)
      .sort((a, b) => new Date(a.closedAt!).getTime() - new Date(b.closedAt!).getTime())
      .slice(-SPARKLINE_POINTS);

    if (closed.length === 0) return [];

    const values = closed.map(w => w.observedConsumptionGalPerMin!);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;
    const step = closed.length > 1 ? SPARKLINE_W / (closed.length - 1) : 0;

    return values.map((value, i) => ({
      x: closed.length === 1 ? SPARKLINE_W / 2 : i * step,
      y: SPARKLINE_H - ((value - min) / span) * SPARKLINE_H,
      value,
    }));
  });

  protected readonly sparklinePath = computed(() =>
    this.sparklinePoints().map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' '),
  );

  protected readonly sparklineWidth = SPARKLINE_W;
  protected readonly sparklineHeight = SPARKLINE_H;

  constructor() {
    const destroyRef = inject(DestroyRef);
    let interval: ReturnType<typeof setInterval> | null = null;

    effect(() => {
      const teamId = this.teamId();
      const carNumber = this.car().number;
      const raceId = this.race().id;

      if (interval !== null) clearInterval(interval);

      void this.refresh(teamId, carNumber, raceId);
      interval = setInterval(() => {
        void this.refresh(teamId, carNumber, raceId);
      }, POLL_INTERVAL_MS);
    });

    destroyRef.onDestroy(() => {
      if (interval !== null) clearInterval(interval);
    });
  }

  protected close(): void {
    this.closed.emit();
  }

  protected onOverrideInput(event: Event): void {
    this.overrideInput.set((event.target as HTMLInputElement).value);
  }

  protected startOverride(): void {
    this.errorMessage.set(null);
    const current = this.calibration()?.value;
    this.overrideInput.set(current != null ? current.toFixed(3) : '1.000');
    this.showOverrideInput.set(true);
  }

  protected cancelOverride(): void {
    this.showOverrideInput.set(false);
    this.overrideInput.set('');
  }

  protected async confirmOverride(): Promise<void> {
    const factor = Number(this.overrideInput());
    if (!Number.isFinite(factor) || factor <= 0) {
      this.errorMessage.set('Factor must be a positive number.');
      return;
    }
    await this.runCalibration(() =>
      this.fuel.saveCalibrationOverride(this.teamId(), this.car().number, { factor }),
    );
    if (!this.errorMessage()) {
      this.showOverrideInput.set(false);
    }
  }

  protected async resetCalibration(): Promise<void> {
    await this.runCalibration(() =>
      this.fuel.resetCalibration(this.teamId(), this.car().number),
    );
  }

  protected async resumeLearning(): Promise<void> {
    await this.runCalibration(() =>
      this.fuel.resumeCalibrationLearning(this.teamId(), this.car().number),
    );
  }

  private async runCalibration(action: () => Promise<CalibrationFactor>): Promise<void> {
    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      const next = await action();
      this.calibration.set(next);
    } catch (err) {
      console.error('Calibration action failed', err);
      this.errorMessage.set('Calibration update failed. Please try again.');
    } finally {
      this.busy.set(false);
    }
  }

  private async refresh(teamId: number, carNumber: string, raceId: number): Promise<void> {
    try {
      const [snapshot, windows, calibration] = await Promise.all([
        this.fuel.loadFuelSnapshot(teamId, carNumber),
        this.fuel.loadFuelWindows(teamId, carNumber, raceId),
        this.fuel.loadCalibrationFactor(teamId, carNumber),
      ]);
      this.snapshot.set(snapshot);
      this.fuelWindows.set(windows);
      this.calibration.set(calibration);
    } catch (err) {
      console.error('Failed to refresh fuel detail data', err);
    }
  }
}

function displayNameFor(name: string): string {
  switch (name) {
    case 'ecu':                 return 'ECU';
    case 'flowmeter.raw':       return 'FlowMeter (raw)';
    case 'flowmeter.corrected': return 'FlowMeter (corrected)';
    case 'pitfill':             return 'PitFill';
    case 'throttle.integral':   return 'Throttle integral';
    case 'throttle.grid':       return 'Throttle grid';
    default:                    return name;
  }
}

function sourceLabel(source: CalibrationFactorSource): string {
  switch (source) {
    case CalibrationFactorSource.Learned:        return 'learned';
    case CalibrationFactorSource.ManualOverride: return 'manual override';
    case CalibrationFactorSource.Reset:          return 'reset';
    default:                                      return 'unknown';
  }
}

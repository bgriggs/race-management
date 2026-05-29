import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { FuelRangeSnapshot } from '../../../../../../shared-ui/src/cloud-api/fuel-range-snapshot';
import { FuelWindow } from '../../../../../../shared-ui/src/cloud-api/fuel-window';
import { Race } from '../../../../../../shared-ui/src/cloud-api/race';
import { RefuelEvent } from '../../../../../../shared-ui/src/cloud-api/refuel-event';
import { Stint } from '../../../../../../shared-ui/src/cloud-api/stint';
import { StintOriginType } from '../../../../../../shared-ui/src/cloud-api/stint-origin-type';
import { CarFuelConfig } from '../../../../../../shared-ui/src/models/car-fuel-config';
import { FuelClient } from '../../../clients/fuel-client';
import { ConfidenceTier } from '../fuel-confidence-toggle/fuel-confidence-toggle';
import { FuelStintModal, StintModalMode } from '../fuel-stint-modal/fuel-stint-modal';
import { GanttScope } from '../fuel-scope-toggle/fuel-scope-toggle';
import { fmtTime } from '../gantt-container/gantt-time';

interface RenderedStint {
  id: number;
  leftPx: number;
  widthPx: number;
  startLabel: string;
  endLabel: string;
  tierManual: boolean;
  isPast: boolean;
  needsVolumeEntry: boolean;
  pendingRefuelEventId: number | null;
}

interface FutureStint {
  index: number;
  leftPx: number;
  widthPx: number;
  startLabel: string;
  endLabel: string;
}

/** Fixed pit-stop gap between consecutive future-projected stints (spec §14). */
const FUTURE_STINT_PIT_GAP_MS = 90_000;

interface ProjectionOverlay {
  kind: 'surplus' | 'shortfall';
  leftPx: number;
  widthPx: number;
  tooltip: string;
}

interface ShortfallWedge {
  leftPx: number;
  widthPx: number;
}

interface ReferenceLine {
  leftPx: number;
}

/** Surplus / shortfall delta floor: render only when |delta| ≥ 1 minute (spec §11, §12). */
const MIN_PROJECTION_DELTA_MIN = 1;

/**
 * Single car row in the Fuel & Pit Strategy gantt. Spec items §5–§7, §17–§19.
 *
 * Four-column grid: car-number column (56px) / gantt canvas (1fr) /
 * right-stat block (110px) / action buttons (~64px). Phase 2 builds the
 * outer structure + right-stat + action buttons; the gantt canvas's
 * positioned children (stint bars, projection overlays, flag stripes,
 * now-line) land in Phase 3.
 */
@Component({
  selector: 'app-fuel-car-row',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FuelStintModal],
  templateUrl: './fuel-car-row.html',
  styleUrl: './fuel-car-row.css',
})
export class FuelCarRow {
  private readonly fuel = inject(FuelClient);

  readonly teamId = input.required<number>();
  readonly car = input.required<Car>();
  readonly race = input.required<Race>();
  readonly selected = input<boolean>(false);
  readonly confidenceTier = input.required<ConfidenceTier>();
  readonly scope = input.required<GanttScope>();
  /** Wall-clock "now"; should be the gantt-container's 1Hz tick. */
  readonly now = input.required<Date>();
  /** Pixels per minute from the gantt-container (already floored at MIN_PX_PER_MINUTE). */
  readonly pxPerMinute = input.required<number>();
  /** UTC ms at `Race.StartTime` — origin of the timeToPx coordinate system. */
  readonly sessionStartMs = input.required<number>();
  /** UTC ms at `Race.EndTime` — used for the scope=end end-line. */
  readonly sessionEndMs = input.required<number>();
  /** Shared horizontal scroll position from the gantt-container. */
  readonly scrollLeft = input.required<number>();

  readonly rowClick = output<string>();
  readonly addStintClick = output<string>();
  readonly settingsClick = output<string>();
  /** Emits when the user scrolls this row's canvas; the parent fans out via the shared signal. */
  readonly scrollLeftChange = output<number>();
  /**
   * Emits this row's canvas content width (scope-aware) so the time-axis at the bottom
   * of the gantt can render ticks across the same width — keeps the scrollbar end aligned
   * with the row's content end and prevents a wider time-axis from creating dead space.
   */
  readonly canvasWidthPxChange = output<number>();

  private readonly canvasWrap = viewChild<ElementRef<HTMLDivElement>>('canvasWrap');

  private readonly snapshot = signal<FuelRangeSnapshot | null>(null);
  private readonly refuelEvents = signal<RefuelEvent[]>([]);
  private readonly fuelWindows = signal<FuelWindow[]>([]);
  private readonly stints = signal<Stint[]>([]);
  /** Fetched once at mount; fuel-config rarely changes during a race session. */
  private readonly carFuelConfig = signal<CarFuelConfig | null>(null);

  /**
   * Stint editor modal state. Opened from:
   * - Add Stint action button (create mode)
   * - Click on a stint bar (edit mode)
   * - Click on the in-bar gas-can icon (edit mode with refuel focus)
   */
  protected readonly modalMode = signal<StintModalMode | null>(null);

  protected readonly carNumber = computed(() => this.car().number);

  /**
   * Active range readout chosen by the Confidence toggle (spec §3, §17).
   * Pulled fresh on every signal update so toggle flips re-render the row.
   */
  private readonly activeRange = computed(() => {
    const s = this.snapshot();
    if (s === null) return null;
    return this.confidenceTier() === 'high' ? s.highConfidence : s.primary;
  });

  /** Currently-open stint = the stint whose [StartAt, EndAt) brackets now (open-ended if no EndAt). */
  private readonly currentStint = computed<Stint | null>(() => {
    const nowMs = this.now().getTime();
    return this.stints().find(s => {
      const start = new Date(s.startAt).getTime();
      if (start > nowMs) return false;
      const end = s.endAt ? new Date(s.endAt).getTime() : Number.POSITIVE_INFINITY;
      return nowMs < end;
    }) ?? null;
  });

  /** Range value text + the "min" suffix split for type-style differentiation. */
  protected readonly rangeMinutesText = computed(() => {
    const r = this.activeRange();
    if (r === null || r.rangeMinutes === null || r.rangeMinutes <= 0) return '—';
    return `${Math.round(r.rangeMinutes)}`;
  });

  /**
   * Delta vs the planned end of the currently-open stint, in whole minutes.
   * Positive = surplus (projected end after planned), negative = shortfall.
   * Null when no current stint or no planned end exists, or when no fuel range
   * data has been computed yet (rangeMinutes ≤ 0).
   */
  protected readonly deltaMinutes = computed<number | null>(() => {
    const range = this.activeRange();
    if (range === null || range.rangeMinutes === null || range.rangeMinutes <= 0) return null;
    const stint = this.currentStint();
    if (stint === null || stint.endAt === null) return null;
    const projectedEndMs = this.now().getTime() + range.rangeMinutes * 60_000;
    const plannedEndMs = new Date(stint.endAt).getTime();
    return Math.round((projectedEndMs - plannedEndMs) / 60_000);
  });

  /** Delta line text per spec §17. */
  protected readonly deltaText = computed(() => {
    const d = this.deltaMinutes();
    if (d === null) return '—';
    if (d === 0) return 'on plan';
    const sign = d > 0 ? '+' : '−';
    return `${sign}${Math.abs(d)} min vs plan`;
  });

  /** Tone for both the range value (top line) and delta line — spec §17. */
  protected readonly deltaTone = computed<'surplus' | 'shortfall' | 'onplan' | 'none'>(() => {
    const d = this.deltaMinutes();
    if (d === null) return 'none';
    if (d > 0) return 'surplus';
    if (d < 0) return 'shortfall';
    return 'onplan';
  });

  /**
   * Minutes since the active FuelWindow opened, for the car-number sub-line.
   * Null when no live telemetry (no snapshot) or no active window.
   */
  protected readonly fuelWindowAgeMin = computed<number | null>(() => {
    const s = this.snapshot();
    if (s === null) return null;
    const opened = s.fuelWindow.openedAtUtc;
    if (opened === null) return null;
    const mins = Math.floor((this.now().getTime() - new Date(opened).getTime()) / 60_000);
    return mins < 0 ? 0 : mins;
  });

  /**
   * Map of `FuelWindow.id` → its start `RefuelEvent` for fast lookup when
   * deciding whether a stint bar needs the in-bar gas-can icon.
   */
  private readonly startRefuelByWindowId = computed(() => {
    const refuelById = new Map(this.refuelEvents().map(r => [r.id, r]));
    const out = new Map<number, RefuelEvent | undefined>();
    for (const w of this.fuelWindows()) {
      out.set(w.id, refuelById.get(w.startRefuelEventId));
    }
    return out;
  });

  /**
   * Stints in chronological order, projected to pixel coordinates. The bar for a
   * stint with `endAt = null` (still actively recording) extends to `now`. The
   * gas-can icon flag is set when the stint is in the past (endAt set AND endAt
   * < now) AND its start RefuelEvent exists with no entered volume yet (Pending
   * status + null volume) AND it's the first stint in its FuelWindow — i.e. a
   * reminder shown only after the driver pits, not during the active stint.
   */
  protected readonly renderedStints = computed<RenderedStint[]>(() => {
    const pxPerMin = this.pxPerMinute();
    const startMs = this.sessionStartMs();
    const nowMs = this.now().getTime();
    const refuelByWindowId = this.startRefuelByWindowId();

    const stintsByWindow = new Map<number, Stint[]>();
    for (const s of this.stints()) {
      const list = stintsByWindow.get(s.fuelWindowId) ?? [];
      list.push(s);
      stintsByWindow.set(s.fuelWindowId, list);
    }
    for (const list of stintsByWindow.values()) {
      list.sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime());
    }

    return [...this.stints()]
      .sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime())
      .map(stint => {
        const startMsAbs = new Date(stint.startAt).getTime();
        const endMsAbs = stint.endAt ? new Date(stint.endAt).getTime() : nowMs;
        const leftPx = ((startMsAbs - startMs) / 60_000) * pxPerMin;
        const widthPx = Math.max(0, ((endMsAbs - startMsAbs) / 60_000) * pxPerMin);

        const isFirstInWindow = stintsByWindow.get(stint.fuelWindowId)?.[0]?.id === stint.id;
        const startRefuel = isFirstInWindow ? refuelByWindowId.get(stint.fuelWindowId) : undefined;
        const isPast = !!stint.endAt && endMsAbs < nowMs;
        const needsVolumeEntry = !!startRefuel
          && isPast
          && startRefuel.enteredFuelGallons === null
          && startRefuel.status === 'Pending';

        return {
          id: stint.id,
          leftPx,
          widthPx,
          startLabel: fmtTime(new Date(stint.startAt)),
          endLabel: stint.endAt ? fmtTime(new Date(stint.endAt)) : '',
          tierManual: stint.originType === StintOriginType.Manual,
          isPast,
          needsVolumeEntry,
          pendingRefuelEventId: needsVolumeEntry ? startRefuel!.id : null,
        };
      });
  });

  /**
   * Projection overlay for scope=current. Surplus/shortfall based on the active
   * range minutes vs the currently-open stint's planned end. Renders only when
   * `|delta| ≥ 1 min` per spec §11/§12.
   */
  protected readonly projectionOverlay = computed<ProjectionOverlay | null>(() => {
    if (this.scope() !== 'current') return null;
    const range = this.activeRange();
    if (range === null || range.rangeMinutes === null || range.rangeMinutes <= 0) return null;
    const stint = this.currentStint();
    if (stint === null || stint.endAt === null) return null;

    const pxPerMin = this.pxPerMinute();
    const startMs = this.sessionStartMs();
    const nowMs = this.now().getTime();
    const projectedEndMs = nowMs + range.rangeMinutes * 60_000;
    const plannedEndMs = new Date(stint.endAt).getTime();
    const deltaMin = Math.round((projectedEndMs - plannedEndMs) / 60_000);

    if (Math.abs(deltaMin) < MIN_PROJECTION_DELTA_MIN) return null;

    if (deltaMin > 0) {
      // Surplus: from planned end to projected end.
      const leftPx = ((plannedEndMs - startMs) / 60_000) * pxPerMin;
      const widthPx = ((projectedEndMs - plannedEndMs) / 60_000) * pxPerMin;
      return {
        kind: 'surplus',
        leftPx,
        widthPx,
        tooltip: `${fmtTime(new Date(projectedEndMs))} · +${deltaMin} min`,
      };
    } else {
      // Shortfall: anchored at planned end, extending LEFT to projected fuel-out
      // time. Symmetric with surplus (which extends right from planned end) — the
      // delta is always measured from the planned end of the stint.
      const leftPx = ((projectedEndMs - startMs) / 60_000) * pxPerMin;
      const widthPx = ((plannedEndMs - projectedEndMs) / 60_000) * pxPerMin;
      return {
        kind: 'shortfall',
        leftPx,
        widthPx,
        tooltip: `${fmtTime(new Date(projectedEndMs))} · −${Math.abs(deltaMin)} min`,
      };
    }
  });

  /** Red-tinted band filling the gap between projected fuel-out and planned stint end (spec §13). */
  protected readonly shortfallWedge = computed<ShortfallWedge | null>(() => {
    const overlay = this.projectionOverlay();
    if (overlay === null || overlay.kind !== 'shortfall') return null;
    const range = this.activeRange();
    const stint = this.currentStint();
    if (!range || range.rangeMinutes === null || !stint || !stint.endAt) return null;
    const pxPerMin = this.pxPerMinute();
    const startMs = this.sessionStartMs();
    const nowMs = this.now().getTime();
    const projectedEndMs = nowMs + range.rangeMinutes * 60_000;
    const plannedEndMs = new Date(stint.endAt).getTime();
    const leftPx = ((projectedEndMs - startMs) / 60_000) * pxPerMin;
    const widthPx = ((plannedEndMs - projectedEndMs) / 60_000) * pxPerMin;
    return { leftPx, widthPx };
  });

  /** Now-line x-position. Null when `now` is before session start. */
  protected readonly nowPx = computed<number | null>(() => {
    const nowMs = this.now().getTime();
    if (nowMs < this.sessionStartMs()) return null;
    return ((nowMs - this.sessionStartMs()) / 60_000) * this.pxPerMinute();
  });

  /** Now-line label text (red, 10px mono per spec §20). */
  protected readonly nowLabel = computed(() => fmtTime(this.now()));

  /**
   * Future projected stints — visible only when scope=end (spec §14). Each stint's
   * duration is `tankCapacity ÷ activeConsumptionRate`. The active rate is derived
   * from the active range readout (`rangeGallons / rangeMinutes`) so it tracks
   * the Confidence toggle. Per spec, flag state is NOT modeled into future
   * projections — we project at the current rate forward. Fixed 90s pit gaps,
   * terminate when the next stint would end at or after Race.EndTime.
   */
  protected readonly futureStints = computed<FutureStint[]>(() => {
    if (this.scope() !== 'end') return [];
    const config = this.carFuelConfig();
    const range = this.activeRange();
    if (!config || !range || range.rangeMinutes === null || range.rangeGallons === null) return [];
    if (range.rangeMinutes <= 0 || range.rangeGallons <= 0) return [];

    const rateGalPerMin = range.rangeGallons / range.rangeMinutes;
    if (!Number.isFinite(rateGalPerMin) || rateGalPerMin <= 0) return [];
    if (config.tankCapacityGallons <= 0) return [];

    const stintDurationMs = (config.tankCapacityGallons / rateGalPerMin) * 60_000;
    if (!Number.isFinite(stintDurationMs) || stintDurationMs <= 0) return [];

    const sessionStartMs = this.sessionStartMs();
    const sessionEndMs = this.sessionEndMs();
    const pxPerMin = this.pxPerMinute();
    const nowMs = Math.max(this.now().getTime(), sessionStartMs);

    // Chain future projections from the END of the last existing stint, not from "now".
    // Starting at now would render a partial bar whose left half is covered by the
    // currently-running (or last-recorded) real stint — visually appearing as a stub
    // duration. The full-tank stintDurationMs is the right duration; the cursor just
    // needs to start past the existing timeline.
    let cursor = nowMs;
    for (const s of this.stints()) {
      const sEndMs = s.endAt ? new Date(s.endAt).getTime() : new Date(s.startAt).getTime();
      if (sEndMs > cursor) cursor = sEndMs;
    }

    const out: FutureStint[] = [];
    let idx = 0;
    // Safety: cap iteration count regardless of math edge cases.
    const MAX_STINTS = 100;
    while (idx < MAX_STINTS && cursor < sessionEndMs) {
      const projectedEndMs = cursor + stintDurationMs;
      // Terminate before the next stint would end at or after session end —
      // but always render the last stint, capped at session end.
      const endMs = Math.min(projectedEndMs, sessionEndMs);
      const leftPx = ((cursor - sessionStartMs) / 60_000) * pxPerMin;
      const widthPx = ((endMs - cursor) / 60_000) * pxPerMin;
      if (widthPx <= 0) break;
      out.push({
        index: idx,
        leftPx,
        widthPx,
        startLabel: fmtTime(new Date(cursor)),
        endLabel: fmtTime(new Date(endMs)),
      });
      if (projectedEndMs >= sessionEndMs) break;
      cursor = projectedEndMs + FUTURE_STINT_PIT_GAP_MS;
      idx++;
    }
    return out;
  });

  /** Start/end reference lines for scope=end (spec §16). */
  protected readonly startLine = computed<ReferenceLine | null>(() =>
    this.scope() === 'end' ? { leftPx: 0 } : null,
  );
  protected readonly endLine = computed<ReferenceLine | null>(() => {
    if (this.scope() !== 'end') return null;
    const leftPx = ((this.sessionEndMs() - this.sessionStartMs()) / 60_000) * this.pxPerMinute();
    return { leftPx };
  });

  /**
   * Total pixel width of the gantt canvas content. In `end` scope (Project-to-end) the
   * canvas extends to `sessionEnd` so future-stint projections and the end-line have
   * room to render. In `current` scope it truncates to the rightmost rendered element
   * — last stint EndAt, or now-line, or projection-overlay end — whichever extends
   * furthest. Without this trim, the canvas inner would always be sessionEnd-wide and
   * the wrap would show empty space past the last bar when content doesn't cover the
   * full session.
   */
  protected readonly canvasWidthPx = computed(() => {
    const startMs = this.sessionStartMs();
    const pxPerMin = this.pxPerMinute();
    if (this.scope() === 'end') {
      return ((this.sessionEndMs() - startMs) / 60_000) * pxPerMin;
    }
    let lastMs = startMs;
    for (const s of this.stints()) {
      const e = s.endAt ? new Date(s.endAt).getTime() : new Date(s.startAt).getTime();
      if (e > lastMs) lastMs = e;
    }
    const nowMs = this.now().getTime();
    if (nowMs > lastMs) lastMs = nowMs;
    const overlay = this.projectionOverlay();
    if (overlay) {
      const overlayRightMs = startMs + ((overlay.leftPx + overlay.widthPx) / pxPerMin) * 60_000;
      if (overlayRightMs > lastMs) lastMs = overlayRightMs;
    }
    return Math.max(0, ((lastMs - startMs) / 60_000) * pxPerMin);
  });

  constructor() {
    const destroyRef = inject(DestroyRef);
    let interval: ReturnType<typeof setInterval> | null = null;
    const POLL_INTERVAL_MS = 5000;

    effect(() => {
      const teamId = this.teamId();
      const carNumber = this.car().number;
      const raceId = this.race().id;

      if (interval !== null) {
        clearInterval(interval);
        interval = null;
      }

      // CarFuelConfig rarely changes during a session — fetch it once on mount
      // (and on car/team change) rather than re-polling alongside the race data.
      void this.refreshCarFuelConfig(teamId, carNumber);
      void this.refreshAll(teamId, carNumber, raceId);
      interval = setInterval(() => void this.refreshAll(teamId, carNumber, raceId), POLL_INTERVAL_MS);
    });

    // Surface canvas width to the parent so the time-axis can match it.
    effect(() => {
      this.canvasWidthPxChange.emit(this.canvasWidthPx());
    });

    // Sync inbound scroll position from the parent's shared signal into this
    // row's canvas wrap. The `!==` guard prevents an infinite scroll-event loop
    // when the parent's update came from this row's own (scroll) handler.
    effect(() => {
      const target = this.scrollLeft();
      const el = this.canvasWrap()?.nativeElement;
      if (el && el.scrollLeft !== target) {
        el.scrollLeft = target;
      }
    });

    destroyRef.onDestroy(() => {
      if (interval !== null) clearInterval(interval);
    });
  }

  protected onCanvasScroll(event: Event): void {
    this.scrollLeftChange.emit((event.target as HTMLDivElement).scrollLeft);
  }

  protected onRowClick(event: MouseEvent): void {
    // Action buttons stop propagation themselves; this fires only for clicks on
    // the row chrome outside the buttons.
    if ((event.target as HTMLElement | null)?.closest('button')) return;
    this.rowClick.emit(this.car().number);
  }

  protected onAddStint(event: Event): void {
    event.stopPropagation();
    this.addStintClick.emit(this.car().number);
    this.modalMode.set({ kind: 'create', previousStintEndAt: this.previousStintEndAt() });
  }

  protected onSettings(event: Event): void {
    event.stopPropagation();
    this.settingsClick.emit(this.car().number);
  }

  /** Click anywhere on a stint bar → open the editor in edit mode. */
  protected onStintBarClick(stintId: number, event: Event): void {
    event.stopPropagation();
    this.openEditModal(stintId, false);
  }

  /** Click on the in-bar gas-can icon → open the editor with refuel focus. */
  protected onGasCanClick(stintId: number, event: Event): void {
    event.stopPropagation();
    this.openEditModal(stintId, true);
  }

  protected closeModal(): void {
    this.modalMode.set(null);
  }

  protected async onModalSaved(): Promise<void> {
    this.modalMode.set(null);
    await this.refreshAll(this.teamId(), this.car().number, this.race().id);
  }

  /**
   * Returns the endAt of the most-recent stint, used by the Add Stint modal to
   * default the new stint's start *time* (the *date* always defaults to today).
   * Null when there's no prior stint or none has a recorded end.
   */
  private previousStintEndAt(): Date | null {
    const stints = this.stints();
    if (stints.length === 0) return null;
    const latest = [...stints].sort((a, b) => new Date(b.startAt).getTime() - new Date(a.startAt).getTime())[0];
    return latest.endAt ? new Date(latest.endAt) : null;
  }

  private openEditModal(stintId: number, focusRefuel: boolean): void {
    const stint = this.stints().find(s => s.id === stintId);
    if (!stint) return;

    const sameWindow = this.stints()
      .filter(s => s.fuelWindowId === stint.fuelWindowId)
      .sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime());
    const isFirstInWindow = sameWindow[0]?.id === stintId;

    const window = this.fuelWindows().find(w => w.id === stint.fuelWindowId);
    const startRefuel = window
      ? this.refuelEvents().find(r => r.id === window.startRefuelEventId) ?? null
      : null;

    this.modalMode.set({ kind: 'edit', stint, startRefuel, isFirstInWindow, focusRefuel });
  }

  private async refreshCarFuelConfig(teamId: number, carNumber: string): Promise<void> {
    try {
      this.carFuelConfig.set(await this.fuel.loadCarFuelConfig(teamId, carNumber));
    } catch (err) {
      console.error('Failed to load CarFuelConfig for car', carNumber, err);
    }
  }

  private async refreshAll(teamId: number, carNumber: string, raceId: number): Promise<void> {
    try {
      const [snapshot, refuels, windows, stints] = await Promise.all([
        this.fuel.loadFuelSnapshot(teamId, carNumber, raceId),
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

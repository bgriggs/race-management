import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Race } from '../../../../../../shared-ui/src/cloud-api/race';
import { RaceSelectionService } from '../../race-selection.service';
import { GanttTimeAxis } from '../gantt-time-axis/gantt-time-axis';

/**
 * Minimum px-per-minute floor. Ensures the shortest plausible stint stays
 * readable even when the session is long enough that fit-to-container would
 * compress the timeline below this density. Spec item §8.
 */
export const MIN_PX_PER_MINUTE = 2.5;

/**
 * Owns the px-per-minute scale, the shared horizontal scroll position, and
 * the gantt's coordinate-system math for the Fuel & Pit Strategy panel.
 * Spec items §5 (gantt scope of car rows), §8 (gantt canvas), §15 (now-line
 * driver), §20 (time-axis scroll synchronization).
 *
 * The 1Hz "now" tick is sourced from the root-owned
 * {@link RaceSelectionService} — we do not spin a second timer here.
 *
 * Phase 1 deliverable: scaffolding only. Rows are projected via &lt;ng-content&gt;;
 * Phase 2's `fuel-car-row` and Phase 4's `gantt-time-axis` consume the
 * exposed scale / now / scroll signals.
 */
@Component({
  selector: 'app-gantt-container',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [GanttTimeAxis],
  templateUrl: './gantt-container.html',
  styleUrl: './gantt-container.css',
})
export class GanttContainer {
  private readonly raceSelection = inject(RaceSelectionService);

  readonly race = input.required<Race>();

  private readonly viewport = viewChild.required<ElementRef<HTMLElement>>('viewport');
  private readonly viewportWidthPx = signal<number>(0);

  /** UTC ms at the start of the session. */
  readonly sessionStartMs = computed(() => new Date(this.race().start).getTime());
  /** UTC ms at the end of the session (start + duration). */
  readonly sessionEndMs = computed(() => this.sessionStartMs() + this.race().duration * 60 * 60 * 1000);
  /** Session length in minutes. */
  readonly sessionDurationMin = computed(() => this.race().duration * 60);

  /**
   * Active pixels-per-minute scale: `max(fitToContainer, MIN_PX_PER_MINUTE)`. When the
   * container is wider than the floor demands, fit-to-container wins; when the floor
   * demands a wider canvas, horizontal scroll kicks in.
   */
  readonly pxPerMinute = computed(() => {
    const w = this.viewportWidthPx();
    const mins = this.sessionDurationMin();
    if (w <= 0 || mins <= 0) return MIN_PX_PER_MINUTE;
    return Math.max(w / mins, MIN_PX_PER_MINUTE);
  });

  /** Total pixel width of the scaled timeline. */
  readonly totalWidthPx = computed(() => this.sessionDurationMin() * this.pxPerMinute());

  /**
   * Active canvas width for the time-axis. Provided by rows (their scope-aware
   * content extent) so the time-axis renders ticks across exactly the same span as
   * the row canvases — otherwise the time-axis scrollbar would extend past where the
   * rows' content ends, recreating the dead-space issue.
   */
  readonly canvasWidthPx = input<number | null>(null);
  readonly resolvedCanvasWidthPx = computed(() =>
    this.canvasWidthPx() ?? this.totalWidthPx(),
  );

  /** Shared 1Hz wall-clock signal — sourced from {@link RaceSelectionService.now}. */
  readonly now = this.raceSelection.now;

  /** Shared horizontal scroll position; children bind two-way for sync. */
  readonly scrollLeft = signal(0);

  constructor() {
    const destroyRef = inject(DestroyRef);

    // ResizeObserver in an afterNextRender effect — but `viewChild.required` resolves
    // in the next change-detection tick, so we wire the observer on first attach via
    // a queueMicrotask. Plain ResizeObserver, not Angular CDK, to keep the dependency
    // graph small.
    queueMicrotask(() => {
      const el = this.viewport().nativeElement;
      this.viewportWidthPx.set(el.clientWidth);

      const ro = new ResizeObserver(entries => {
        for (const entry of entries) {
          this.viewportWidthPx.set(entry.contentRect.width);
        }
      });
      ro.observe(el);
      destroyRef.onDestroy(() => ro.disconnect());
    });
  }

  /**
   * Pixel offset of a wall-clock instant from `Race.StartTime`. Negative for
   * pre-session times; clamped at the call site if rendering requires that.
   */
  timeToPx(instantMs: number): number {
    return ((instantMs - this.sessionStartMs()) / 60_000) * this.pxPerMinute();
  }

  /** Bound from row scroll handlers; updates the shared position iff changed. */
  setScrollLeft(value: number): void {
    if (this.scrollLeft() !== value) this.scrollLeft.set(value);
  }
}

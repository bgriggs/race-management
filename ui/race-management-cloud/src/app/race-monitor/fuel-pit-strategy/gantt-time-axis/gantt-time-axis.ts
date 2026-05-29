import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { fmtTime } from '../gantt-container/gantt-time';

interface HourTick {
  ms: number;
  leftPx: number;
  label: string;
}

const MS_PER_HOUR = 3_600_000;
const MS_PER_MINUTE = 60_000;

/**
 * Time axis row that lives at the bottom of the gantt-container (spec §20).
 *
 * Renders one hour-boundary tick across the session timeline plus a red
 * "now" label that tracks the now-line position at 1 Hz. Shares horizontal
 * scroll with every car-row gantt canvas via the shared `scrollLeft`
 * signal in the parent gantt-container.
 */
@Component({
  selector: 'app-gantt-time-axis',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './gantt-time-axis.html',
  styleUrl: './gantt-time-axis.css',
})
export class GanttTimeAxis {
  readonly sessionStartMs = input.required<number>();
  readonly sessionEndMs = input.required<number>();
  readonly pxPerMinute = input.required<number>();
  readonly now = input.required<Date>();
  readonly scrollLeft = input.required<number>();

  readonly scrollLeftChange = output<number>();

  private readonly wrap = viewChild<ElementRef<HTMLDivElement>>('wrap');

  protected readonly canvasWidthPx = computed(() =>
    ((this.sessionEndMs() - this.sessionStartMs()) / MS_PER_MINUTE) * this.pxPerMinute(),
  );

  protected readonly ticks = computed<HourTick[]>(() => {
    const startMs = this.sessionStartMs();
    const endMs = this.sessionEndMs();
    const pxPerMin = this.pxPerMinute();

    // First tick = ceil(start) to the next whole hour; last = floor(end) to the previous.
    const firstTickMs = Math.ceil(startMs / MS_PER_HOUR) * MS_PER_HOUR;
    const lastTickMs = Math.floor(endMs / MS_PER_HOUR) * MS_PER_HOUR;
    if (firstTickMs > lastTickMs) return [];

    const ticks: HourTick[] = [];
    for (let t = firstTickMs; t <= lastTickMs; t += MS_PER_HOUR) {
      ticks.push({
        ms: t,
        leftPx: ((t - startMs) / MS_PER_MINUTE) * pxPerMin,
        label: fmtTime(new Date(t)),
      });
    }
    return ticks;
  });

  protected readonly nowPx = computed<number | null>(() => {
    const nowMs = this.now().getTime();
    if (nowMs < this.sessionStartMs()) return null;
    return ((nowMs - this.sessionStartMs()) / MS_PER_MINUTE) * this.pxPerMinute();
  });

  protected readonly nowLabel = computed(() => fmtTime(this.now()));

  constructor() {
    // Sync inbound scroll position from the container to the DOM element.
    effect(() => {
      const target = this.scrollLeft();
      const el = this.wrap()?.nativeElement;
      if (el && el.scrollLeft !== target) {
        el.scrollLeft = target;
      }
    });
  }

  protected onScroll(event: Event): void {
    this.scrollLeftChange.emit((event.target as HTMLDivElement).scrollLeft);
  }
}

import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { Race } from '../../../../../../shared-ui/src/cloud-api/race';
import { RefuelEvent } from '../../../../../../shared-ui/src/cloud-api/refuel-event';
import { Stint } from '../../../../../../shared-ui/src/cloud-api/stint';
import { StintOriginType } from '../../../../../../shared-ui/src/cloud-api/stint-origin-type';
import { FuelClient } from '../../../clients/fuel-client';

/**
 * Mode discriminator for the stint editor modal.
 *
 * - `create` — Add Stint button on a row. Fields: start, end, tier. Submitting
 *   POSTs to `save-stint` which opens a new FuelWindow + pending RefuelEvent.
 * - `edit` — clicking an existing stint bar (or its in-bar gas-can icon).
 *   Adds refuel-volume and "No fuel on pit" controls plus a Delete button.
 *   The `focusRefuel` flag lets callers (e.g. gas-can click) request the
 *   volume field to be focused on open.
 */
export type StintModalMode =
  | { kind: 'create'; previousStintEndAt: Date | null }
  | { kind: 'edit'; stint: Stint; startRefuel: RefuelEvent | null; isFirstInWindow: boolean; focusRefuel?: boolean };

@Component({
  selector: 'app-fuel-stint-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fuel-stint-modal.html',
  styleUrl: './fuel-stint-modal.css',
})
export class FuelStintModal {
  private readonly fuel = inject(FuelClient);

  readonly teamId = input.required<number>();
  readonly car = input.required<Car>();
  readonly race = input.required<Race>();
  readonly mode = input.required<StintModalMode>();

  readonly closed = output<void>();
  readonly saved = output<void>();

  protected readonly startAt = signal('');
  protected readonly endAt = signal('');
  protected readonly originType = signal<StintOriginType>(StintOriginType.Manual);
  protected readonly refuelGallons = signal('');
  protected readonly noFuelOnPit = signal(false);

  protected readonly submitting = signal(false);
  protected readonly deleting = signal(false);
  protected readonly confirmDelete = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly isEdit = computed(() => this.mode().kind === 'edit');
  protected readonly title = computed(() => this.isEdit() ? 'Edit Stint' : 'Add Stint');

  protected readonly StintOriginType = StintOriginType;

  /**
   * The Delete button is disabled when "No fuel on pit" is checked at the same
   * time the stint is first-in-its-window — the cascade rules for delete
   * collide with the rules for uncheck; require the engineer to settle one
   * cascade before the other. (Keeping this simple in v1.)
   */
  protected readonly canSubmit = computed(() => {
    if (this.submitting()) return false;
    if (!this.startAt()) return false;
    return true;
  });

  /** Refuel-volume field is hidden while "No fuel on pit" is checked (mutually exclusive). */
  protected readonly showRefuelField = computed(() => this.isEdit() && !this.noFuelOnPit());

  constructor() {
    effect(() => {
      const mode = this.mode();
      this.confirmDelete.set(false);
      this.errorMessage.set(null);

      const tz = this.race().timeZone;
      if (mode.kind === 'edit') {
        this.startAt.set(toLocalDatetimeInput(new Date(mode.stint.startAt)));
        // For a still-running stint (endAt = null), prefill with "now" so the
        // engineer can save as "ends now" with one click or nudge the time.
        this.endAt.set(
          mode.stint.endAt
            ? toLocalDatetimeInput(new Date(mode.stint.endAt))
            : toDatetimeLocalInTz(new Date(), tz),
        );
        this.originType.set(mode.stint.originType);
        this.refuelGallons.set(
          mode.startRefuel?.enteredFuelGallons != null ? String(mode.startRefuel.enteredFuelGallons) : '',
        );
        this.noFuelOnPit.set(!mode.isFirstInWindow);
      } else {
        // Create defaults (all wall-clock in race TZ):
        //   - Start: today's date, time = previous stint's end time (HH:MM only).
        //     Falls back to race-TZ current clock time when there's no prior stint.
        //   - End:   today's date, time = race-TZ current clock time.
        // Tier defaults to Manual.
        const prev = mode.previousStintEndAt;
        const [todayDate, nowTime] = toDatetimeLocalInTz(new Date(), tz).split('T');
        const pad = (n: number) => String(n).padStart(2, '0');
        const startTime = prev
          ? `${pad(prev.getHours())}:${pad(prev.getMinutes())}`
          : nowTime;
        this.startAt.set(`${todayDate}T${startTime}`);
        this.endAt.set(`${todayDate}T${nowTime}`);
        this.originType.set(StintOriginType.Manual);
        this.refuelGallons.set('');
        this.noFuelOnPit.set(false);
      }
    });
  }

  protected onStartChange(event: Event): void { this.startAt.set((event.target as HTMLInputElement).value); }
  protected onEndChange(event: Event): void { this.endAt.set((event.target as HTMLInputElement).value); }
  protected onTierChange(value: StintOriginType): void { this.originType.set(value); }
  protected onRefuelGallonsChange(event: Event): void { this.refuelGallons.set((event.target as HTMLInputElement).value); }
  protected onNoFuelOnPitChange(event: Event): void { this.noFuelOnPit.set((event.target as HTMLInputElement).checked); }

  protected cancel(): void {
    if (this.submitting() || this.deleting()) return;
    this.closed.emit();
  }

  protected async submit(): Promise<void> {
    if (!this.canSubmit()) return;
    this.submitting.set(true);
    this.errorMessage.set(null);

    try {
      const startAt = parseNaiveDatetimeLocal(this.startAt());
      const endAt = this.endAt() ? parseNaiveDatetimeLocal(this.endAt()) : null;

      if (endAt && endAt <= startAt) {
        this.errorMessage.set('End time must be after start time.');
        this.submitting.set(false);
        return;
      }

      const mode = this.mode();
      if (mode.kind === 'create') {
        await this.fuel.saveStint(this.teamId(), {
          carNumber: this.car().number,
          raceId: this.race().id,
          startAt,
          endAt,
          originType: this.originType(),
        });
      } else {
        const updated = await this.fuel.updateStint(this.teamId(), mode.stint.id, {
          startAt,
          endAt,
          originType: this.originType(),
          noFuelOnPit: this.noFuelOnPit(),
        });

        // Refuel-volume update is a separate endpoint when the value changed
        // and the stint still starts a FuelWindow after the noFuelOnPit cascade.
        // When `noFuelOnPit` is checked, the FuelWindow's start RefuelEvent has
        // been merged away — there's nothing to set a volume on.
        if (!this.noFuelOnPit()) {
          const gallons = Number(this.refuelGallons());
          const original = mode.startRefuel?.enteredFuelGallons ?? null;
          if (this.refuelGallons() !== '' && Number.isFinite(gallons) && gallons > 0 && gallons !== original) {
            // Phase 5 v1: re-fetch the stint's window-start refuel id from the
            // freshly-saved stint by reloading. Simpler: just call the existing
            // enterRefuelVolume endpoint with the original id when it was a
            // first-in-window stint and stays one.
            if (mode.startRefuel && mode.isFirstInWindow) {
              await this.fuel.enterRefuelVolume(this.teamId(), mode.startRefuel.id, { gallons });
            }
          }
        }

        void updated;
      }

      this.saved.emit();
    } catch (err) {
      console.error('Failed to save stint:', err);
      this.errorMessage.set('Save failed. Check the values and try again.');
    } finally {
      this.submitting.set(false);
    }
  }

  protected requestDelete(): void {
    this.confirmDelete.set(true);
  }

  protected cancelDelete(): void {
    this.confirmDelete.set(false);
  }

  protected async confirmAndDelete(): Promise<void> {
    const mode = this.mode();
    if (mode.kind !== 'edit') return;
    this.deleting.set(true);
    this.errorMessage.set(null);
    try {
      await this.fuel.deleteStint(this.teamId(), mode.stint.id);
      this.saved.emit();
    } catch (err) {
      console.error('Failed to delete stint:', err);
      this.errorMessage.set('Delete failed. If this is the first stint with later stints in the same fuel window, delete the later ones first.');
      this.confirmDelete.set(false);
    } finally {
      this.deleting.set(false);
    }
  }

  /**
   * Inline cascade warning shown above the delete button. Helps the engineer
   * predict what will happen.
   */
  protected readonly deleteWarning = computed(() => {
    const mode = this.mode();
    if (mode.kind !== 'edit') return null;
    if (mode.isFirstInWindow) {
      return 'This stint owns its fuel window. Deleting will also remove the refuel record that opened it.';
    }
    return null;
  });
}

/** Format a Date for an <input type="datetime-local"> value (local time, no seconds). */
function toLocalDatetimeInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Formats a Date as an `<input type="datetime-local">` value (`YYYY-MM-DDTHH:MM`)
 * expressing the wall-clock in the given IANA timezone — not the browser's.
 * Used for "now" defaults so a user whose browser TZ ≠ race TZ still sees the
 * race's current local time when adding the first stint.
 */
function toDatetimeLocalInTz(d: Date, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).formatToParts(d);
  const get = (type: string) => parts.find(p => p.type === type)?.value ?? '00';
  // Some ICU versions emit "24" for midnight under hour12:false; normalize.
  const hour = get('hour') === '24' ? '00' : get('hour');
  return `${get('year')}-${get('month')}-${get('day')}T${hour}:${get('minute')}`;
}

/**
 * Parses an `<input type="datetime-local">` string into a Date whose UTC
 * components match the typed wall-clock — i.e., NOT a browser-local
 * interpretation. Used on submit so the JSON payload carries the literal
 * wall-clock the engineer entered, matching the `Race.Start` "naive
 * wall-clock in race timezone" convention from design.md.
 *
 * Why this matters: `new Date("2026-05-27T00:00")` interprets the string as
 * the browser's local timezone, then serializes to UTC (with a Z suffix).
 * That breaks the round-trip when browser TZ ≠ race TZ — and even when they
 * match, the server reads it back as naive and the offset gets re-applied
 * on display, shifting the time by `(browser TZ offset)` each save.
 */
function parseNaiveDatetimeLocal(s: string): Date {
  const [date, time] = s.split('T');
  const [y, mo, d] = date.split('-').map(Number);
  const [h, mi] = time.split(':').map(Number);
  return new Date(Date.UTC(y, mo - 1, d, h, mi));
}

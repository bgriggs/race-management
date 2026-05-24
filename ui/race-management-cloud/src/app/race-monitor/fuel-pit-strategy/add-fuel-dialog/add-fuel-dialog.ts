import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { FuelClient } from '../../../clients/fuel-client';

export type AddFuelDialogMode =
  | { kind: 'manual' }
  | { kind: 'enter-volume'; refuelEventId: number; defaultAt: Date };

@Component({
  selector: 'app-add-fuel-dialog',
  imports: [],
  templateUrl: './add-fuel-dialog.html',
  styleUrl: './add-fuel-dialog.css',
})
export class AddFuelDialog {
  private readonly fuel = inject(FuelClient);

  readonly teamId = input.required<number>();
  readonly car = input.required<Car>();
  readonly mode = input.required<AddFuelDialogMode>();

  readonly closed = output<void>();
  readonly saved = output<void>();

  protected readonly gallons = signal('');
  protected readonly atUtc = signal('');
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly isEnterVolume = computed(() => this.mode().kind === 'enter-volume');
  protected readonly title = computed(() => this.isEnterVolume() ? 'Enter Fuel Volume' : 'Record Manual Refuel');

  protected readonly canSubmit = computed(() => {
    const g = Number(this.gallons());
    return Number.isFinite(g) && g > 0 && !this.submitting();
  });

  constructor() {
    effect(() => {
      const mode = this.mode();
      const defaultAt = mode.kind === 'enter-volume' ? mode.defaultAt : new Date();
      this.atUtc.set(toLocalDatetimeInput(defaultAt));
      this.gallons.set('');
      this.errorMessage.set(null);
    });
  }

  protected onGallonsInput(event: Event): void {
    this.gallons.set((event.target as HTMLInputElement).value);
  }

  protected onAtUtcInput(event: Event): void {
    this.atUtc.set((event.target as HTMLInputElement).value);
  }

  protected cancel(): void {
    this.closed.emit();
  }

  protected async submit(): Promise<void> {
    if (!this.canSubmit()) return;
    const gallons = Number(this.gallons());
    const mode = this.mode();
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      if (mode.kind === 'enter-volume') {
        await this.fuel.enterRefuelVolume(this.teamId(), mode.refuelEventId, { gallons });
      } else {
        const atUtc = this.atUtc() ? new Date(this.atUtc()) : null;
        await this.fuel.saveManualRefuel(this.teamId(), this.car().number, {
          gallons,
          atUtc,
        });
      }
      this.saved.emit();
    } catch (err) {
      console.error('Failed to save refuel entry:', err);
      this.errorMessage.set('Save failed. Check the gallons value and that a race is active.');
    } finally {
      this.submitting.set(false);
    }
  }
}

/** Format a Date for an <input type="datetime-local"> value (local time, no seconds). */
function toLocalDatetimeInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

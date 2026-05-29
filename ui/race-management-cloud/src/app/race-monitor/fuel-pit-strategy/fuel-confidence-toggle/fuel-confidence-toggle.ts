import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { PillToggle } from '../../../../../../shared-ui/src/lib/pill-toggle/pill-toggle';

export type ConfidenceTier = 'regular' | 'high';

/**
 * Confidence-tier toggle (Regular ↔ High confidence) for the Fuel & Pit Strategy
 * panel. Spec item §3. View-state only; affects projection overlays, right-stat
 * range values, and right-stat deltas across every car row simultaneously.
 *
 * Thin wrapper over the generic <rm-pill-toggle> — this component owns the
 * semantic mapping (`regular` ↔ `left`, `high` ↔ `right`) so callers don't deal
 * in left/right vocabulary.
 */
@Component({
  selector: 'app-fuel-confidence-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PillToggle],
  template: `
    <rm-pill-toggle
      leftLabel="Regular"
      rightLabel="High confidence"
      ariaLabel="Toggle fuel projection confidence tier"
      [value]="side()"
      (valueChange)="onSide($event)"
    />
  `,
})
export class FuelConfidenceToggle {
  readonly value = input.required<ConfidenceTier>();
  readonly valueChange = output<ConfidenceTier>();

  protected readonly side = computed<'left' | 'right'>(() => this.value() === 'regular' ? 'left' : 'right');

  protected onSide(side: 'left' | 'right'): void {
    this.valueChange.emit(side === 'left' ? 'regular' : 'high');
  }
}

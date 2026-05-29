import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * Two-state pill toggle: a labeled switch with the inactive label on one side,
 * the active label on the other, and a draggable-looking knob between them.
 * Click either label or the track to flip; tab + space/enter for keyboard.
 *
 * Generic enough to reuse for any pair of mutually exclusive view-state choices.
 * The Fuel & Pit Strategy panel layers `fuel-confidence-toggle` and
 * `fuel-scope-toggle` on top of this for semantics-bearing naming.
 */
@Component({
  selector: 'rm-pill-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="track"
      role="switch"
      [attr.aria-checked]="value() === 'right'"
      [attr.aria-label]="ariaLabel()"
      (click)="flip()"
    >
      <span class="label left" [class.active]="value() === 'left'" (click)="select('left', $event)">{{ leftLabel() }}</span>
      <span class="knob" [class.right]="value() === 'right'" aria-hidden="true"></span>
      <span class="label right" [class.active]="value() === 'right'" (click)="select('right', $event)">{{ rightLabel() }}</span>
    </button>
  `,
  styleUrl: './pill-toggle.css',
})
export class PillToggle {
  readonly leftLabel = input.required<string>();
  readonly rightLabel = input.required<string>();
  readonly value = input.required<'left' | 'right'>();
  readonly ariaLabel = input<string>('Toggle');

  readonly valueChange = output<'left' | 'right'>();

  protected flip(): void {
    this.valueChange.emit(this.value() === 'left' ? 'right' : 'left');
  }

  protected select(side: 'left' | 'right', event: Event): void {
    event.stopPropagation();
    if (this.value() !== side) this.valueChange.emit(side);
  }
}

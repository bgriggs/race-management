import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { PillToggle } from '../../../../../../shared-ui/src/lib/pill-toggle/pill-toggle';

export type GanttScope = 'current' | 'end';

/**
 * Scope toggle (Current ↔ Project to end) for the Fuel & Pit Strategy panel.
 * Spec item §4. View-state only; switches the gantt between the current
 * FuelWindow's projection overlay and the full sequence of projected future
 * stints from now to session end.
 */
@Component({
  selector: 'app-fuel-scope-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PillToggle],
  template: `
    <rm-pill-toggle
      leftLabel="Current"
      rightLabel="Project to end"
      ariaLabel="Toggle gantt projection scope"
      [value]="side()"
      (valueChange)="onSide($event)"
    />
  `,
})
export class FuelScopeToggle {
  readonly value = input.required<GanttScope>();
  readonly valueChange = output<GanttScope>();

  protected readonly side = computed<'left' | 'right'>(() => this.value() === 'current' ? 'left' : 'right');

  protected onSide(side: 'left' | 'right'): void {
    this.valueChange.emit(side === 'left' ? 'current' : 'end');
  }
}

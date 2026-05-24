import { Component, computed, input } from '@angular/core';
import { RefuelConfidenceTier } from '../../../../../../shared-ui/src/cloud-api/refuel-confidence-tier';

export type StintBoundary = 'first' | 'new-fuel-window' | 'non-fuel-pit';

@Component({
  selector: '[appGanttBar]',
  imports: [],
  templateUrl: './gantt-bar.html',
  styleUrl: './gantt-bar.css',
})
export class GanttBar {
  readonly xPct = input.required<number>();
  readonly widthPct = input.required<number>();
  readonly boundary = input.required<StintBoundary>();
  readonly confidenceTier = input<RefuelConfidenceTier | null>(null);
  readonly stintLabel = input<string>('');

  protected readonly fillClass = computed(() => {
    const tier = this.confidenceTier();
    switch (tier) {
      case RefuelConfidenceTier.High:    return 'tier-high';
      case RefuelConfidenceTier.Medium:  return 'tier-medium';
      case RefuelConfidenceTier.Low:     return 'tier-low';
      case RefuelConfidenceTier.Manual:  return 'tier-manual';
      default:                            return 'tier-unknown';
    }
  });

  protected readonly icon = computed(() => {
    switch (this.boundary()) {
      case 'new-fuel-window': return '⛽';
      case 'non-fuel-pit':    return '🅿';
      default:                return '';
    }
  });
}

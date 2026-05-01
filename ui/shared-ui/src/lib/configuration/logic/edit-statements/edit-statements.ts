import { Component, computed, input, model } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ComparisonDefinition } from '../../../../models/comparison-definition';
import { StatementDefinition } from '../../../../models/statement-definition';
import { EditComparisonsList } from '../edit-comparisons-list/edit-comparisons-list';

type StatementMode = 'momentary' | 'activateDeactivate';

@Component({
  selector: 'lib-edit-statements',
  standalone: true,
  imports: [EditComparisonsList],
  templateUrl: './edit-statements.html',
  styleUrl: './edit-statements.css',
})
export class EditStatements {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);

  readonly statement = model<StatementDefinition>(this.createEmptyStatement());

  readonly mode = computed<StatementMode>(() =>
    this.statement().deactivateComparisons === null ? 'momentary' : 'activateDeactivate'
  );

  readonly activateComparisons = computed<ComparisonDefinition[]>(() => {
    const activateGroups = this.statement().activateComparisons;
    if (!activateGroups[0]) {
      return [];
    }

    return activateGroups[0];
  });

  readonly deactivateComparisons = computed<ComparisonDefinition[]>(() => {
    const deactivateGroups = this.statement().deactivateComparisons;
    if (!deactivateGroups || !deactivateGroups[0]) {
      return [];
    }

    return deactivateGroups[0];
  });

  onModeChanged(mode: StatementMode): void {
    if (mode === 'momentary') {
      this.statement.set({
        ...this.statement(),
        deactivateComparisons: null,
      });
      return;
    }

    const currentDeactivate = this.statement().deactivateComparisons;
    this.statement.set({
      ...this.statement(),
      deactivateComparisons: currentDeactivate ?? [[]],
    });
  }

  onActivateComparisonsChanged(comparisons: ComparisonDefinition[]): void {
    const current = this.statement();
    const remainingGroups = current.activateComparisons.slice(1);

    this.statement.set({
      ...current,
      activateComparisons: [comparisons, ...remainingGroups],
    });
  }

  onDeactivateComparisonsChanged(comparisons: ComparisonDefinition[]): void {
    const current = this.statement();
    const existingDeactivate = current.deactivateComparisons ?? [];
    const remainingGroups = existingDeactivate.slice(1);

    this.statement.set({
      ...current,
      deactivateComparisons: [comparisons, ...remainingGroups],
    });
  }

  private createEmptyStatement(): StatementDefinition {
    return {
      id: '',
      activateComparisons: [[]],
      deactivateComparisons: null,
    };
  }

}

import { Component, computed, input, model, signal } from '@angular/core';
import { createGuid } from '../../../utils/guid';
import { MatIcon } from '@angular/material/icon';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ComparisonDefinition } from '../../../../models/comparison-definition';
import { LogicType } from '../../../../models/logic-type';
import { EditComparison } from '../edit-comparison/edit-comparison';

@Component({
  selector: 'lib-edit-comparisons-list',
  standalone: true,
  imports: [MatIcon, EditComparison],
  templateUrl: './edit-comparisons-list.html',
  styleUrl: './edit-comparisons-list.css',
})
export class EditComparisonsList {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);

  readonly comparisons = model<ComparisonDefinition[]>([]);

  readonly editingIndex = signal<number | 'new' | null>(null);
  readonly draftComparison = signal<ComparisonDefinition | null>(null);

  readonly hasComparisons = computed(() => this.comparisons().length > 0);
  readonly isEditing = computed(() => this.editingIndex() !== null);

  readonly editorUsedChannelIds = computed(() => {
    const currentEditIndex = this.editingIndex();
    const currentComparisons = this.comparisons();
    const usedByCurrentList = currentComparisons
      .flatMap((comparison, index) => {
        if (currentEditIndex !== 'new' && currentEditIndex === index) {
          return [];
        }

        const ids: string[] = [];
        if (comparison.channelId) {
          ids.push(comparison.channelId);
        }

        if (comparison.channelComparisonId) {
          ids.push(comparison.channelComparisonId);
        }

        return ids;
      });

    return [...this.usedChannelIds(), ...usedByCurrentList];
  });

  startAdd(): void {
    this.editingIndex.set('new');
    this.draftComparison.set(this.createEmptyComparison());
  }

  startEdit(index: number): void {
    const existing = this.comparisons()[index];
    if (!existing) {
      return;
    }

    this.editingIndex.set(index);
    this.draftComparison.set({ ...existing });
  }

  stopEdit(): void {
    this.editingIndex.set(null);
    this.draftComparison.set(null);
  }

  onDraftComparisonChanged(value: ComparisonDefinition): void {
    this.draftComparison.set(value);
  }

  saveDraft(): void {
    const draft = this.draftComparison();
    if (!draft) {
      return;
    }

    const normalized = {
      ...draft,
      id: draft.id || createGuid(),
    };

    const currentComparisons = this.comparisons();
    const currentEditIndex = this.editingIndex();

    if (currentEditIndex === 'new' || currentEditIndex === null) {
      this.comparisons.set([...currentComparisons, normalized]);
      this.stopEdit();
      return;
    }

    const updated = currentComparisons.map((comparison, index) =>
      index === currentEditIndex ? normalized : comparison
    );

    this.comparisons.set(updated);
    this.stopEdit();
  }

  deleteComparison(index: number): void {
    this.comparisons.set(this.comparisons().filter((_, i) => i !== index));

    if (this.editingIndex() === index) {
      this.stopEdit();
    }
  }

  getChannelName(channelId: string | null): string {
    if (!channelId) {
      return '';
    }

    return this.channels().find((channel) => channel.id === channelId)?.name ?? 'Unknown Channel';
  }

  getConditionLabel(logic: LogicType): string {
    switch (logic) {
      case LogicType.GreaterThan:
        return '>';
      case LogicType.LessThan:
        return '<';
      case LogicType.GreaterThanOrEqualTo:
        return '>=';
      case LogicType.LessThanOrEqualTo:
        return '<=';
      case LogicType.EqualTo:
        return '=';
      case LogicType.True:
        return 'True';
      case LogicType.False:
        return 'False';
      case LogicType.Updated:
        return 'Updated';
      case LogicType.ChangedBy:
        return 'Changed By';
      default:
        return '';
    }
  }

  getComparisonTargetLabel(comparison: ComparisonDefinition): string {
    if (comparison.useStaticComparison) {
      return comparison.staticValueComparison;
    }

    return this.getChannelName(comparison.channelComparisonId);
  }

  private createEmptyComparison(): ComparisonDefinition {
    return {
      id: '',
      channelId: '',
      logic: LogicType.GreaterThan,
      useStaticComparison: true,
      staticValueComparison: '',
      channelComparisonId: null,
      forMs: 0,
      reverseResult: false,
    };
  }

}

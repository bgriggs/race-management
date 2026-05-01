import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { ComparisonDefinition } from '../../../../models/comparison-definition';
import { ConditionDefinition } from '../../../../models/condition-definition';
import { StatementDefinition } from '../../../../models/statement-definition';
import { ChannelUsageService } from '../../channels/channel-usage.service';
import { EditUserCondition } from '../edit-user-condition/edit-user-condition';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

@Component({
  selector: 'lib-user-conditions-list',
  standalone: true,
  imports: [MatIcon, EditUserCondition],
  templateUrl: './user-conditions-list.html',
  styleUrl: './user-conditions-list.css',
})
export class UserConditionsList {
  private readonly channelUsageService = inject(ChannelUsageService);

  readonly configuration = input<CarConfiguration | null>(null);
  readonly userConditionsChange = output<ConditionDefinition[]>();

  readonly editingConditionId = signal<string | 'new' | null>(null);
  readonly draftCondition = signal<ConditionDefinition | null>(null);

  readonly conditions = computed<ConditionDefinition[]>(() => this.configuration()?.userConditions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);
  readonly outputChannelUsedIds = computed(() => {
    const editingId = this.editingConditionId();
    const conditions = this.conditions().filter((condition) =>
      editingId && editingId !== 'new' ? condition.id !== editingId : true
    );

    return this.channelUsageService.getUsedChannelIdsFromUserConditions(conditions);
  });
  readonly hasConditions = computed(() => this.conditions().length > 0);
  readonly isEditing = computed(() => this.editingConditionId() !== null);

  startAdd(): void {
    this.editingConditionId.set('new');
    this.draftCondition.set(this.createEmptyCondition());
  }

  startEdit(conditionId: string): void {
    const existing = this.conditions().find((condition) => condition.id === conditionId);
    if (!existing) {
      return;
    }

    this.editingConditionId.set(conditionId);
    this.draftCondition.set(this.cloneCondition(existing));
  }

  stopEdit(): void {
    this.editingConditionId.set(null);
    this.draftCondition.set(null);
  }

  onDraftConditionChanged(condition: ConditionDefinition): void {
    this.draftCondition.set(condition);
  }

  saveDraft(): void {
    const draft = this.draftCondition();
    if (!draft) {
      return;
    }

    const normalized = this.normalizeCondition({
      ...draft,
      id: draft.id || this.createGuid(),
      name: draft.name.trim(),
    });

    const existingConditions = this.conditions();
    const editingId = this.editingConditionId();

    if (editingId === 'new' || editingId === null) {
      this.userConditionsChange.emit([...existingConditions, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existingConditions.map((condition) =>
      condition.id === editingId ? normalized : condition
    );

    this.userConditionsChange.emit(updated);
    this.stopEdit();
  }

  deleteCondition(conditionId: string): void {
    this.userConditionsChange.emit(this.conditions().filter((condition) => condition.id !== conditionId));

    if (this.editingConditionId() === conditionId) {
      this.stopEdit();
    }
  }

  getOutputChannelName(channelId: string): string {
    return this.channels().find((channel) => channel.id === channelId)?.name ?? 'Unknown Channel';
  }

  private normalizeCondition(condition: ConditionDefinition): ConditionDefinition {
    return {
      ...condition,
      id: condition.id || this.createGuid(),
      outputChannelId: condition.outputChannelId || EMPTY_GUID,
      statements: condition.statements.map((statement) => this.normalizeStatement(statement)),
    };
  }

  private normalizeStatement(statement: StatementDefinition): StatementDefinition {
    return {
      ...statement,
      id: statement.id || this.createGuid(),
      activateComparisons: statement.activateComparisons.map((group) =>
        group.map((comparison) => this.normalizeComparison(comparison))
      ),
      deactivateComparisons: statement.deactivateComparisons
        ? statement.deactivateComparisons.map((group) =>
            group.map((comparison) => this.normalizeComparison(comparison))
          )
        : null,
    };
  }

  private normalizeComparison(comparison: ComparisonDefinition): ComparisonDefinition {
    return {
      ...comparison,
      id: comparison.id || this.createGuid(),
      channelId: comparison.channelId || EMPTY_GUID,
      channelComparisonId: comparison.channelComparisonId || null,
    };
  }

  private createEmptyCondition(): ConditionDefinition {
    return {
      id: '',
      name: '',
      statements: [
        {
          id: '',
          activateComparisons: [[]],
          deactivateComparisons: null,
        },
      ],
      outputChannelId: '',
    };
  }

  private cloneCondition(condition: ConditionDefinition): ConditionDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(condition);
    }

    return JSON.parse(JSON.stringify(condition)) as ConditionDefinition;
  }

  private createGuid(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = Math.floor(Math.random() * 16);
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }

}

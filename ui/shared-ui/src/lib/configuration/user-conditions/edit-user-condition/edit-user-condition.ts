import { Component, computed, input, model, signal } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ConditionDefinition } from '../../../../models/condition-definition';
import { StatementDefinition } from '../../../../models/statement-definition';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';
import { EditStatements } from '../../logic/edit-statements/edit-statements';

@Component({
  selector: 'lib-edit-user-condition',
  standalone: true,
  imports: [EditStatements, ChannelSelector],
  templateUrl: './edit-user-condition.html',
  styleUrl: './edit-user-condition.css',
})
export class EditUserCondition {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);

  readonly condition = model<ConditionDefinition>(this.createEmptyCondition());

  readonly isNameDirty = signal(false);
  readonly isNameValid = computed(() => {
    const trimmedLength = this.condition().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });

  readonly firstStatement = computed<StatementDefinition>(() => {
    const statement = this.condition().statements[0];
    if (statement) {
      return statement;
    }

    return this.createEmptyStatement();
  });

  onStatementChanged(statement: StatementDefinition): void {
    const currentCondition = this.condition();
    const remainingStatements = currentCondition.statements.slice(1);

    this.condition.set({
      ...currentCondition,
      statements: [statement, ...remainingStatements],
    });
  }

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
    this.condition.set({
      ...this.condition(),
      name: value,
    });
  }

  onOutputChannelChanged(channelId: string | null): void {
    this.condition.set({
      ...this.condition(),
      outputChannelId: channelId ?? '',
    });
  }

  private createEmptyCondition(): ConditionDefinition {
    return {
      id: '',
      name: '',
      statements: [this.createEmptyStatement()],
      outputChannelId: '',
    };
  }

  private createEmptyStatement(): StatementDefinition {
    return {
      id: '',
      activateComparisons: [[]],
      deactivateComparisons: null,
    };
  }

}

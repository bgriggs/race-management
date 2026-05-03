import { Component, input, model } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { StatementDefinition } from '../../../../models/statement-definition';
import { TimerDefinition } from '../../../../models/timer-definition';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';
import { EditStatements } from '../../logic/edit-statements/edit-statements';

@Component({
  selector: 'lib-edit-timer',
  standalone: true,
  imports: [ChannelSelector, EditStatements],
  templateUrl: './edit-timer.html',
  styleUrl: './edit-timer.css',
})
export class EditTimer {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);

  readonly timer = model<TimerDefinition>(this.createEmptyTimer());

  onNameChanged(value: string): void {
    this.timer.set({
      ...this.timer(),
      name: value,
    });
  }

  onOutputChannelChanged(channelId: string | null): void {
    this.timer.set({
      ...this.timer(),
      outputChId: channelId ?? '',
    });
  }

  onStatementChanged(statement: StatementDefinition): void {
    this.timer.set({
      ...this.timer(),
      statement: this.normalizeStatement(statement),
    });
  }

  private normalizeStatement(statement: StatementDefinition): StatementDefinition {
    return {
      ...statement,
      id: statement.id || this.createGuid(),
      activateComparisons: statement.activateComparisons.length
        ? statement.activateComparisons
        : [[]],
      deactivateComparisons: statement.deactivateComparisons,
    };
  }

  private createEmptyTimer(): TimerDefinition {
    return {
      id: '',
      name: '',
      outputChId: '',
      statement: this.createEmptyStatement(),
      countDown: false,
      enableRollover: false,
      rolloverSeconds: 0,
      enableStartSeconds: false,
      startSeconds: 0,
      enableStopSeconds: false,
      stopSeconds: 0,
    };
  }

  private createEmptyStatement(id = ''): StatementDefinition {
    return {
      id,
      activateComparisons: [[]],
      deactivateComparisons: null,
    };
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

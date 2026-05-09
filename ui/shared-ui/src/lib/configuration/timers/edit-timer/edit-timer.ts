import { Component, computed, input, model, signal } from '@angular/core';
import { createGuid } from '../../../utils/guid';
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
  readonly isNameDirty = signal(false);
  readonly isNameValid = computed(() => {
    const trimmedLength = this.timer().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
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

  onCountDownChanged(value: boolean): void {
    this.timer.set({
      ...this.timer(),
      countDown: value,
    });
  }

  onEnableRolloverChanged(value: boolean): void {
    this.timer.set({
      ...this.timer(),
      enableRollover: value,
    });
  }

  onRolloverSecondsChanged(value: number): void {
    this.timer.set({
      ...this.timer(),
      rolloverSeconds: value,
    });
  }

  onEnableStartSecondsChanged(value: boolean): void {
    this.timer.set({
      ...this.timer(),
      enableStartSeconds: value,
    });
  }

  onStartSecondsChanged(value: number): void {
    this.timer.set({
      ...this.timer(),
      startSeconds: value,
    });
  }

  onEnableStopSecondsChanged(value: boolean): void {
    this.timer.set({
      ...this.timer(),
      enableStopSeconds: value,
    });
  }

  onStopSecondsChanged(value: number): void {
    this.timer.set({
      ...this.timer(),
      stopSeconds: value,
    });
  }

  private normalizeStatement(statement: StatementDefinition): StatementDefinition {
    return {
      ...statement,
      id: statement.id || createGuid(),
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

}

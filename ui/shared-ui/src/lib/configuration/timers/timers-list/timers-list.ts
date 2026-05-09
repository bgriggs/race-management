import { Component, computed, input, output, signal } from '@angular/core';
import { createGuid } from '../../../utils/guid';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { StatementDefinition } from '../../../../models/statement-definition';
import { TimerDefinition } from '../../../../models/timer-definition';
import { EditTimer } from '../edit-timer/edit-timer';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

@Component({
  selector: 'lib-timers-list',
  standalone: true,
  imports: [MatIcon, EditTimer],
  templateUrl: './timers-list.html',
  styleUrl: './timers-list.css',
})
export class TimersList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly timerDefinitionsChange = output<TimerDefinition[]>();

  readonly editingTimerId = signal<string | 'new' | null>(null);
  readonly draftTimer = signal<TimerDefinition | null>(null);

  readonly timers = computed<TimerDefinition[]>(() => this.configuration()?.timerDefinitions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);
  readonly outputChannelUsedIds = computed(() => {
    const editingId = this.editingTimerId();
    return this.timers()
      .filter((timer) => (editingId && editingId !== 'new' ? timer.id !== editingId : true))
      .map((timer) => timer.outputChId)
      .filter((channelId) => !!channelId);
  });
  readonly isDraftNameValid = computed(() => {
    const draft = this.draftTimer();
    if (!draft) {
      return false;
    }

    const trimmedLength = draft.name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly hasTimers = computed(() => this.timers().length > 0);
  readonly isEditing = computed(() => this.editingTimerId() !== null);

  startAdd(): void {
    this.editingTimerId.set('new');
    this.draftTimer.set(this.createEmptyTimer());
  }

  startEdit(timerId: string): void {
    const existing = this.timers().find((timer) => timer.id === timerId);
    if (!existing) {
      return;
    }

    this.editingTimerId.set(timerId);
    this.draftTimer.set(this.cloneTimer(existing));
  }

  stopEdit(): void {
    this.editingTimerId.set(null);
    this.draftTimer.set(null);
  }

  onDraftTimerChanged(timer: TimerDefinition): void {
    this.draftTimer.set(timer);
  }

  saveDraft(): void {
    const draft = this.draftTimer();
    if (!draft || !this.isDraftNameValid()) {
      return;
    }

    const normalized = this.normalizeTimer({
      ...draft,
      id: draft.id || createGuid(),
      name: draft.name.trim(),
    });

    const existingTimers = this.timers();
    const editingId = this.editingTimerId();

    if (editingId === 'new' || editingId === null) {
      this.timerDefinitionsChange.emit([...existingTimers, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existingTimers.map((timer) => (timer.id === editingId ? normalized : timer));
    this.timerDefinitionsChange.emit(updated);
    this.stopEdit();
  }

  deleteTimer(timerId: string): void {
    this.timerDefinitionsChange.emit(this.timers().filter((timer) => timer.id !== timerId));

    if (this.editingTimerId() === timerId) {
      this.stopEdit();
    }
  }

  getOutputChannelName(channelId: string): string {
    return this.channels().find((channel) => channel.id === channelId)?.name ?? 'Unknown Channel';
  }

  private normalizeTimer(timer: TimerDefinition): TimerDefinition {
    return {
      ...timer,
      id: timer.id || createGuid(),
      outputChId: timer.outputChId || EMPTY_GUID,
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

  private createEmptyStatement(): StatementDefinition {
    return {
      id: '',
      activateComparisons: [[]],
      deactivateComparisons: null,
    };
  }

  private cloneTimer(timer: TimerDefinition): TimerDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(timer);
    }

    return JSON.parse(JSON.stringify(timer)) as TimerDefinition;
  }

}

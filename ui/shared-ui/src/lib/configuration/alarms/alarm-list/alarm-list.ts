import { Component, computed, input, output, signal } from '@angular/core';
import { createGuid } from '../../../utils/guid';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { AlarmDefinition } from '../../../../models/alarm-definition';
import { StatementDefinition } from '../../../../models/statement-definition';
import { EditAlarm } from '../edit-alarm/edit-alarm';

@Component({
  selector: 'lib-alarm-list',
  standalone: true,
  imports: [MatIcon, EditAlarm],
  templateUrl: './alarm-list.html',
  styleUrl: './alarm-list.css',
})
export class AlarmList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly alarmDefinitionsChange = output<AlarmDefinition[]>();

  readonly editingAlarmId = signal<string | 'new' | null>(null);
  readonly draftAlarm = signal<AlarmDefinition | null>(null);

  readonly alarms = computed<AlarmDefinition[]>(() => this.configuration()?.alarmDefinitions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);
  readonly isDraftNameValid = computed(() => {
    const draft = this.draftAlarm();
    if (!draft) {
      return false;
    }

    const trimmedLength = draft.name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly hasAlarms = computed(() => this.alarms().length > 0);
  readonly isEditing = computed(() => this.editingAlarmId() !== null);

  startAdd(): void {
    this.editingAlarmId.set('new');
    this.draftAlarm.set(this.createEmptyAlarm());
  }

  startEdit(alarmId: string): void {
    const existing = this.alarms().find((alarm) => alarm.id === alarmId);
    if (!existing) {
      return;
    }

    this.editingAlarmId.set(alarmId);
    this.draftAlarm.set(this.cloneAlarm(existing));
  }

  stopEdit(): void {
    this.editingAlarmId.set(null);
    this.draftAlarm.set(null);
  }

  onDraftAlarmChanged(alarm: AlarmDefinition): void {
    this.draftAlarm.set(alarm);
  }

  saveDraft(): void {
    const draft = this.draftAlarm();
    if (!draft || !this.isDraftNameValid()) {
      return;
    }

    const normalized: AlarmDefinition = {
      ...draft,
      id: draft.id || createGuid(),
      name: draft.name.trim(),
    };

    const existingAlarms = this.alarms();
    const editingId = this.editingAlarmId();

    if (editingId === 'new' || editingId === null) {
      this.alarmDefinitionsChange.emit([...existingAlarms, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existingAlarms.map((alarm) =>
      alarm.id === editingId ? normalized : alarm
    );

    this.alarmDefinitionsChange.emit(updated);
    this.stopEdit();
  }

  deleteAlarm(alarmId: string): void {
    this.alarmDefinitionsChange.emit(this.alarms().filter((alarm) => alarm.id !== alarmId));

    if (this.editingAlarmId() === alarmId) {
      this.stopEdit();
    }
  }

  private createEmptyAlarm(): AlarmDefinition {
    return {
      id: '',
      name: '',
      statement: this.createEmptyStatement(),
      messsage: '',
      displayChannelSourceColorHex: '',
      alarmStatusChannelId: null,
      timeAfterAckToDisplaySecs: 0,
    };
  }

  private createEmptyStatement(): StatementDefinition {
    return {
      id: createGuid(),
      activateComparisons: [[]],
      deactivateComparisons: null,
    };
  }

  private cloneAlarm(alarm: AlarmDefinition): AlarmDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(alarm);
    }

    return JSON.parse(JSON.stringify(alarm)) as AlarmDefinition;
  }

}

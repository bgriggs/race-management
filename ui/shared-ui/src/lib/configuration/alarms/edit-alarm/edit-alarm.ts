import { Component, computed, input, model, signal } from '@angular/core';
import { AlarmDefinition } from '../../../../models/alarm-definition';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { StatementDefinition } from '../../../../models/statement-definition';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';
import { EditStatements } from '../../logic/edit-statements/edit-statements';

@Component({
  selector: 'lib-edit-alarm',
  standalone: true,
  imports: [ChannelSelector, EditStatements],
  templateUrl: './edit-alarm.html',
  styleUrl: './edit-alarm.css',
})
export class EditAlarm {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);
  readonly alarm = model.required<AlarmDefinition>();

  readonly isNameDirty = signal(false);
  readonly isNameValid = computed(() => {
    const trimmedLength = this.alarm().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly isMessageValid = computed(() => this.alarm().messsage.length <= 20);

  readonly isColorEnabled = computed(() => this.alarm().displayChannelSourceColorHex.length > 0);
  readonly colorRgbHex = computed(() => {
    const hex = this.alarm().displayChannelSourceColorHex;
    return hex.length >= 7 ? hex.slice(0, 7) : '#ff0000';
  });
  readonly colorAlphaPercent = computed(() => {
    const hex = this.alarm().displayChannelSourceColorHex;
    if (hex.length === 9) {
      return Math.round((parseInt(hex.slice(7, 9), 16) / 255) * 100);
    }
    return 100;
  });

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
    this.alarm.set({ ...this.alarm(), name: value });
  }

  onMessageChanged(value: string): void {
    this.alarm.set({ ...this.alarm(), messsage: value });
  }

  onStatementChanged(statement: StatementDefinition): void {
    this.alarm.set({ ...this.alarm(), statement });
  }

  onTimeAfterAckChanged(value: number): void {
    this.alarm.set({ ...this.alarm(), timeAfterAckToDisplaySecs: value });
  }

  onAlarmStatusChannelChanged(channelId: string | null): void {
    this.alarm.set({ ...this.alarm(), alarmStatusChannelId: channelId });
  }

  onColorEnabledChanged(enabled: boolean): void {
    this.alarm.set({
      ...this.alarm(),
      displayChannelSourceColorHex: enabled ? '#ff0000ff' : '',
    });
  }

  onColorRgbChanged(rgb: string): void {
    const current = this.alarm().displayChannelSourceColorHex;
    const alphaPart = current.length === 9 ? current.slice(7, 9) : 'ff';
    this.alarm.set({ ...this.alarm(), displayChannelSourceColorHex: rgb + alphaPart });
  }

  onColorAlphaChanged(percent: number): void {
    const clamped = Math.max(0, Math.min(100, percent));
    const alphaHex = Math.round((clamped / 100) * 255).toString(16).padStart(2, '0');
    const current = this.alarm().displayChannelSourceColorHex;
    const rgbPart = current.length >= 7 ? current.slice(0, 7) : '#ff0000';
    this.alarm.set({ ...this.alarm(), displayChannelSourceColorHex: rgbPart + alphaHex });
  }
}

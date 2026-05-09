import { Component, computed, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { LoggingDefinition } from '../../../../models/logging-definition';
import { LoggingFrequency } from '../../../../models/logging-frequency';
import { EditLogEntry } from '../edit-log-entry/edit-log-entry';

const FREQUENCY_LABELS: Record<LoggingFrequency, string> = {
  [LoggingFrequency.OncePerSecond]: '1 Hz',
  [LoggingFrequency.TwicePerSecond]: '2 Hz',
  [LoggingFrequency.FiveTimesPerSecond]: '5 Hz',
  [LoggingFrequency.TenTimesPerSecond]: '10 Hz',
  [LoggingFrequency.TwentyTimesPerSecond]: '20 Hz',
};

@Component({
  selector: 'lib-log-list',
  standalone: true,
  imports: [MatIcon, EditLogEntry],
  templateUrl: './log-list.html',
  styleUrl: './log-list.css',
})
export class LogList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly loggingDefinitionsChange = output<LoggingDefinition[]>();

  readonly editingLogId = signal<string | 'new' | null>(null);
  readonly draftLog = signal<LoggingDefinition | null>(null);

  readonly logs = computed<LoggingDefinition[]>(() => this.configuration()?.loggingDefinitions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);

  readonly hasLogs = computed(() => this.logs().length > 0);
  readonly isEditing = computed(() => this.editingLogId() !== null);
  readonly isDraftValid = computed(() => !!this.draftLog()?.channelId);

  startAdd(): void {
    this.editingLogId.set('new');
    this.draftLog.set({ id: '', channelId: '', frequency: LoggingFrequency.OncePerSecond });
  }

  startEdit(logId: string): void {
    const existing = this.logs().find((log) => log.id === logId);
    if (!existing) return;
    this.editingLogId.set(logId);
    this.draftLog.set(structuredClone(existing));
  }

  stopEdit(): void {
    this.editingLogId.set(null);
    this.draftLog.set(null);
  }

  onDraftChanged(entry: LoggingDefinition): void {
    this.draftLog.set(entry);
  }

  saveDraft(): void {
    const draft = this.draftLog();
    if (!draft || !this.isDraftValid()) return;

    const normalized: LoggingDefinition = {
      ...draft,
      id: draft.id || this.createGuid(),
    };

    const existing = this.logs();
    const editingId = this.editingLogId();

    if (editingId === 'new' || editingId === null) {
      this.loggingDefinitionsChange.emit([...existing, normalized]);
    } else {
      this.loggingDefinitionsChange.emit(existing.map((log) => log.id === editingId ? normalized : log));
    }

    this.stopEdit();
  }

  deleteLog(logId: string): void {
    this.loggingDefinitionsChange.emit(this.logs().filter((log) => log.id !== logId));
    if (this.editingLogId() === logId) {
      this.stopEdit();
    }
  }

  getChannelName(channelId: string): string {
    return this.channels().find((channel) => channel.id === channelId)?.name ?? 'Unknown Channel';
  }

  getFrequencyLabel(frequency: LoggingFrequency): string {
    return FREQUENCY_LABELS[frequency] ?? String(frequency);
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

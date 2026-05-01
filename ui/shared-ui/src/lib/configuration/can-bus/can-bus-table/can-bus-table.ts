import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatIcon } from '@angular/material/icon';
import { CanBusInterfaceConfig } from '../../../../models/can-bus-interface-config';
import { CanMessageConfig } from '../../../../models/can-message-config';
import { CanChannelAssignmentConfig } from '../../../../models/can-channel-assignment-config';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { CanBusChannelAssignment } from '../can-bus-channel-assignment/can-bus-channel-assignment';
import { CanBusEditMessage } from '../can-bus-edit-message/can-bus-edit-message';
import { ChannelUsageService } from '../../channels/channel-usage.service';

const BIT_RATES: { label: string; value: number }[] = [
  { label: '1 Mbs', value: 1000000 },
  { label: '800 Kbs', value: 800000 },
  { label: '500 Kbs', value: 500000 },
  { label: '250 Kbs', value: 250000 },
  { label: '125 Kbs', value: 125000 },
  { label: '100 Kbs', value: 100000 },
  { label: '50 Kbs', value: 50000 },
  { label: '25 Kbs', value: 25000 },
  { label: '10 Kbs', value: 10000 },
];

@Component({
  selector: 'lib-can-bus-table',
  imports: [FormsModule, MatSlideToggleModule, MatIcon, CanBusChannelAssignment, CanBusEditMessage],
  templateUrl: './can-bus-table.html',
  styleUrl: './can-bus-table.css',
})
export class CanBusTable {
  private readonly channelUsageService = inject(ChannelUsageService);

  readonly config = input.required<CanBusInterfaceConfig>();
  readonly channels = input<ChannelDefinition[]>([]);
  readonly configChange = output<CanBusInterfaceConfig>();

  readonly bitRates = BIT_RATES;
  readonly byteIndices = [0, 1, 2, 3, 4, 5, 6, 7];

  /** Index of the message being edited, or 'new' for a new message, or null when closed */
  readonly editingMessageIndex = signal<number | 'new' | null>(null);

  readonly editingMessage = computed<CanMessageConfig | null>(() => {
    const idx = this.editingMessageIndex();
    if (idx === null || idx === 'new') return null;
    return this.config().messages[idx] ?? null;
  });

  readonly isEditing = computed(() => this.editingMessageIndex() !== null);

  /** Track which byte assignment is being edited: { msgIndex, byteIndex } or null */
  readonly editingAssignment = signal<{ msgIndex: number; byteIndex: number } | null>(null);

  readonly editingAssignmentConfig = computed<CanChannelAssignmentConfig | null>(() => {
    const editing = this.editingAssignment();
    if (!editing) return null;
    const msg = this.config().messages[editing.msgIndex];
    if (!msg) return null;
    // Find an assignment whose range covers this byte
    return msg.channelAssignments.find(a =>
      editing.byteIndex >= a.offset && editing.byteIndex < a.offset + a.length
    ) ?? null;
  });

  readonly editingMessageAssignments = computed<CanChannelAssignmentConfig[]>(() => {
    const editing = this.editingAssignment();
    if (!editing) return [];
    const msg = this.config().messages[editing.msgIndex];
    return msg?.channelAssignments ?? [];
  });

  readonly isEditingAssignment = computed(() => this.editingAssignment() !== null);

  readonly usedChannelIds = computed(() => {
    return this.channelUsageService.getUsedChannelIdsFromCanInterfaces([this.config()]);
  });

  onInterfaceNameChange(value: string): void {
    this.emitConfig({ interfaceName: value });
  }

  onBitRateChange(value: string): void {
    this.emitConfig({ bitRate: Number(value) });
  }

  onSilentChange(checked: boolean): void {
    this.emitConfig({ silentOnCanBus: checked });
  }

  onMessageEnabledChange(index: number, checked: boolean): void {
    this.emitMessageChange(index, { isEnabled: checked });
  }

  addMessage(): void {
    this.editingMessageIndex.set('new');
  }

  startEditMessage(index: number): void {
    this.editingMessageIndex.set(index);
  }

  stopEditMessage(): void {
    this.editingMessageIndex.set(null);
  }

  saveMessage(message: CanMessageConfig): void {
    const idx = this.editingMessageIndex();
    if (idx === 'new') {
      this.emitConfig({ messages: [...this.config().messages, message] });
    } else if (typeof idx === 'number') {
      const messages = this.config().messages.map((msg, i) => i === idx ? message : msg);
      this.emitConfig({ messages });
    }
    this.stopEditMessage();
  }

  deleteMessage(index: number): void {
    if (!confirm('Are you sure you want to delete this message?')) return;
    const messages = this.config().messages.filter((_, i) => i !== index);
    this.emitConfig({ messages });
  }

  formatCanId(msg: CanMessageConfig): string {
    const digits = msg.isExtended ? 8 : 3;
    return '0x' + msg.canId.toString(16).toUpperCase().padStart(digits, '0');
  }

  /** Build a layout of visible byte cells for a message row, supporting colspan for multi-byte assignments */
  getByteLayout(msg: CanMessageConfig): ByteCell[] {
    const cells: ByteCell[] = [];
    let b = 0;
    while (b < 8) {
      if (b >= msg.length) {
        cells.push({ type: 'disabled', byteIndex: b, colspan: 1, channelName: '' });
        b++;
        continue;
      }
      const assignment = msg.channelAssignments.find(a =>
        b >= a.offset && b < a.offset + a.length
      );
      if (assignment && b === assignment.offset) {
        // Start of a multi-byte assignment
        const span = Math.min(assignment.length, msg.length - b);
        const ch = this.channels().find(c => c.id === assignment.id);
        cells.push({
          type: 'assigned',
          byteIndex: b,
          colspan: span,
          channelName: ch?.name ?? 'Unknown',
        });
        b += span;
      } else if (assignment) {
        // Middle of an assignment — skip (covered by colspan)
        b++;
      } else {
        cells.push({ type: 'unassigned', byteIndex: b, colspan: 1, channelName: 'Unassigned' });
        b++;
      }
    }
    return cells;
  }

  startEditAssignment(msgIndex: number, byteIndex: number): void {
    this.editingAssignment.set({ msgIndex, byteIndex });
  }

  stopEditAssignment(): void {
    this.editingAssignment.set(null);
  }

  saveAssignment(assignment: CanChannelAssignmentConfig | null): void {
    const editing = this.editingAssignment();
    if (!editing) return;
    const msg = this.config().messages[editing.msgIndex];
    // Remove the assignment that covers the clicked byte (may have different offset if multi-byte)
    const existingAssignment = msg.channelAssignments.find(a =>
      editing.byteIndex >= a.offset && editing.byteIndex < a.offset + a.length
    );
    let assignments = existingAssignment
      ? msg.channelAssignments.filter(a => a !== existingAssignment)
      : [...msg.channelAssignments];
    if (assignment) {
      assignments = [...assignments, assignment];
    }
    this.emitMessageChange(editing.msgIndex, { channelAssignments: assignments });
    this.stopEditAssignment();
  }

  private emitConfig(partial: Partial<CanBusInterfaceConfig>): void {
    this.configChange.emit({ ...this.config(), ...partial });
  }

  private emitMessageChange(index: number, partial: Partial<CanMessageConfig>): void {
    const messages = this.config().messages.map((msg, i) =>
      i === index ? { ...msg, ...partial } : msg
    );
    this.emitConfig({ messages });
  }
}

export interface ByteCell {
  type: 'assigned' | 'unassigned' | 'disabled';
  byteIndex: number;
  colspan: number;
  channelName: string;
}

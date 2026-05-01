import { Component, computed, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { CanMessageConfig } from '../../../../models/can-message-config';

const TRANSMIT_FREQUENCY_OPTIONS: { label: string; value: string }[] = [
  { label: '0.5hz', value: '00:00:02' },
  { label: '1hz', value: '00:00:01' },
  { label: '2hz', value: '00:00:00.5000000' },
  { label: '5hz', value: '00:00:00.2000000' },
  { label: '10hz', value: '00:00:00.1000000' },
  { label: '25hz', value: '00:00:00.0400000' },
  { label: '50hz', value: '00:00:00.0200000' },
];

@Component({
  selector: 'lib-can-bus-edit-message',
  imports: [FormsModule, MatSlideToggleModule],
  templateUrl: './can-bus-edit-message.html',
  styleUrl: './can-bus-edit-message.css',
})
export class CanBusEditMessage implements OnInit {
  readonly message = input<CanMessageConfig | null>(null);
  readonly save = output<CanMessageConfig>();
  readonly cancel = output<void>();

  readonly isExtended = signal(false);
  readonly canIdHex = signal('000');
  readonly dlc = signal(8);
  readonly isBigEndian = signal(true);
  readonly isReceive = signal(true);
  readonly transmitRate = signal('00:00:01');

  readonly canIdMaxLength = computed(() => this.isExtended() ? 8 : 3);

  readonly canIdError = computed(() => {
    const hex = this.canIdHex().trim();
    if (!hex) return 'CAN ID is required.';
    if (!/^[0-9a-fA-F]+$/.test(hex)) return 'CAN ID must be a hex value.';
    const maxDigits = this.isExtended() ? 8 : 3;
    if (hex.length > maxDigits) return `CAN ID must be at most ${maxDigits} hex digits for ${this.isExtended() ? '29-bit' : '11-bit'} IDs.`;
    const value = parseInt(hex, 16);
    const maxValue = this.isExtended() ? 0x1FFFFFFF : 0x7FF;
    if (value > maxValue) return `CAN ID exceeds maximum value for ${this.isExtended() ? '29-bit' : '11-bit'} IDs.`;
    return null;
  });

  readonly isValid = computed(() => !this.canIdError());

  readonly dlcOptions = [1, 2, 3, 4, 5, 6, 7, 8];
  readonly transmitFrequencyOptions = TRANSMIT_FREQUENCY_OPTIONS;

  ngOnInit(): void {
    const msg = this.message();
    if (msg) {
      this.isExtended.set(msg.isExtended);
      const digits = msg.isExtended ? 8 : 3;
      this.canIdHex.set(msg.canId.toString(16).toUpperCase().padStart(digits, '0'));
      this.dlc.set(msg.length);
      this.isBigEndian.set(msg.isBigEndian);
      this.isReceive.set(msg.isReceive);
      this.transmitRate.set(msg.transmitRate || '00:00:01');
    }
  }

  onExtendedToggle(checked: boolean): void {
    this.isExtended.set(checked);
    // Re-pad the hex value for the new width
    const hex = this.canIdHex().trim();
    const digits = checked ? 8 : 3;
    const truncated = hex.slice(0, digits);
    this.canIdHex.set(truncated.padStart(digits, '0'));
  }

  onSave(): void {
    if (!this.isValid()) return;

    const existing = this.message();
    const result: CanMessageConfig = {
      isEnabled: existing?.isEnabled ?? true,
      canId: parseInt(this.canIdHex().trim(), 16),
      isExtended: this.isExtended(),
      length: this.dlc(),
      isBigEndian: this.isBigEndian(),
      isReceive: this.isReceive(),
      transmitRate: this.transmitRate(),
      channelAssignments: existing?.channelAssignments ?? [],
    };
    this.save.emit(result);
  }

  onCancel(): void {
    this.cancel.emit();
  }
}

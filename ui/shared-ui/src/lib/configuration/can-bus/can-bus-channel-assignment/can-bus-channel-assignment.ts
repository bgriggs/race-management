import { Component, computed, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { CanChannelAssignmentConfig } from '../../../../models/can-channel-assignment-config';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';

@Component({
  selector: 'lib-can-bus-channel-assignment',
  imports: [FormsModule, MatSlideToggleModule, ChannelSelector],
  templateUrl: './can-bus-channel-assignment.html',
  styleUrl: './can-bus-channel-assignment.css',
})
export class CanBusChannelAssignment implements OnInit {
  readonly assignment = input<CanChannelAssignmentConfig | null>(null);
  readonly byteIndex = input.required<number>();
  readonly channels = input<ChannelDefinition[]>([]);
  readonly usedChannelIds = input<string[]>([]);
  readonly siblingAssignments = input<CanChannelAssignmentConfig[]>([]);
  readonly save = output<CanChannelAssignmentConfig | null>();
  readonly cancel = output<void>();

  readonly channelId = signal<string | null>(null);
  readonly offset = signal(0);
  readonly length = signal(1);
  readonly maskHex = signal('FF');
  readonly isSigned = signal(false);
  readonly multiplier = signal(1);
  readonly divider = signal(1);
  readonly constant = signal(0);

  readonly lengthOptions = computed(() => {
    const max = 8 - this.offset();
    return Array.from({ length: max }, (_, i) => i + 1);
  });

  readonly defaultMask = computed(() => 'FF'.repeat(this.length()));

  readonly overlapError = computed<string | null>(() => {
    const currentOffset = this.offset();
    const currentLength = this.length();
    const currentEnd = currentOffset + currentLength - 1;
    const currentAssignment = this.assignment();

    for (const sibling of this.siblingAssignments()) {
      // Skip the assignment we're editing
      if (currentAssignment && sibling.offset === currentAssignment.offset && sibling.id === currentAssignment.id) {
        continue;
      }
      const siblingEnd = sibling.offset + sibling.length - 1;
      if (currentOffset <= siblingEnd && currentEnd >= sibling.offset) {
        const ch = this.channels().find(c => c.id === sibling.id);
        const name = ch?.name ?? 'Unknown';
        return `Overlaps with "${name}" at byte ${sibling.offset + 1}. Unassign it first.`;
      }
    }
    return null;
  });

  readonly isValid = computed(() => !this.overlapError());

  ngOnInit(): void {
    const a = this.assignment();
    if (a) {
      this.channelId.set(a.id);
      this.offset.set(a.offset);
      this.length.set(a.length);
      this.maskHex.set(a.mask.toString(16).toUpperCase().padStart(a.length * 2, '0'));
      this.isSigned.set(a.isSigned);
      this.multiplier.set(a.formulaMultiplier);
      this.divider.set(a.formulaDivider);
      this.constant.set(a.formulaConst);
    } else {
      this.offset.set(this.byteIndex());
    }
  }

  onLengthChange(value: number): void {
    this.length.set(value);
    this.maskHex.set('FF'.repeat(value));
  }

  onSave(): void {
    if (!this.isValid()) return;

    const id = this.channelId();
    if (!id) {
      this.save.emit(null);
      return;
    }

    const mask = parseInt(this.maskHex().trim() || this.defaultMask(), 16) || 0xFF;
    this.save.emit({
      id,
      offset: this.offset(),
      length: this.length(),
      mask,
      isSigned: this.isSigned(),
      formulaMultiplier: this.multiplier(),
      formulaDivider: this.divider(),
      formulaConst: this.constant(),
    });
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onClear(): void {
    this.save.emit(null);
  }
}

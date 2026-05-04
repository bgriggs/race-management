import { Component, computed, input, model, signal } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { CounterDefinition } from '../../../../models/counter-definition';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';

@Component({
  selector: 'lib-edit-counter',
  standalone: true,
  imports: [ChannelSelector],
  templateUrl: './edit-counter.html',
  styleUrl: './edit-counter.css',
})
export class EditCounter {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);

  readonly counter = model<CounterDefinition>(this.createEmptyCounter());
  readonly isNameDirty = signal(false);
  readonly isNameValid = computed(() => {
    const trimmedLength = this.counter().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly isMinValueValid = computed(() => this.counter().minValue >= -2147483648);
  readonly isMaxValueValid = computed(() => this.counter().maxValue <= 2147483647);

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
    this.counter.set({
      ...this.counter(),
      name: value,
    });
  }

  onOutputChannelChanged(channelId: string | null): void {
    this.counter.set({
      ...this.counter(),
      outputChId: channelId ?? '',
    });
  }

  onUpChannelChanged(channelId: string | null): void {
    this.counter.set({ ...this.counter(), upChId: channelId ?? '' });
  }

  onDownChannelChanged(channelId: string | null): void {
    this.counter.set({ ...this.counter(), downChId: channelId ?? '' });
  }

  onResetChannelChanged(channelId: string | null): void {
    this.counter.set({ ...this.counter(), resetChId: channelId ?? '' });
  }

  onMaxValueChanged(value: number): void {
    this.counter.set({ ...this.counter(), maxValue: value });
  }

  onMinValueChanged(value: number): void {
    this.counter.set({ ...this.counter(), minValue: value });
  }

  onRollAtLimitChanged(value: boolean): void {
    this.counter.set({ ...this.counter(), rollAtLimit: value });
  }

  onStartValueChanged(value: number): void {
    this.counter.set({ ...this.counter(), startValue: value });
  }

  onPersistValueChanged(value: boolean): void {
    this.counter.set({ ...this.counter(), persistValue: value });
  }

  private createEmptyCounter(): CounterDefinition {
    return {
      id: '',
      name: '',
      outputChId: '',
      upChId: '',
      downChId: '',
      resetChId: '',
      maxValue: 2147483647,
      minValue: 0,
      rollAtLimit: false,
      startValue: 0,
      persistValue: false,
    };
  }

}

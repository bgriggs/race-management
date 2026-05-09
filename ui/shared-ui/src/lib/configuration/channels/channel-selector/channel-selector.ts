import { Component, computed, input, model, signal } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ChannelSelectionList } from '../channel-selection-list/channel-selection-list';

@Component({
  selector: 'lib-channel-selector',
  imports: [ChannelSelectionList],
  templateUrl: './channel-selector.html',
  styleUrl: './channel-selector.css',
})
export class ChannelSelector {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);
  readonly channelId = model<string | null>(null);

  readonly showList = signal(false);

  readonly selectedChannel = computed(() => {
    const id = this.channelId();
    if (!id) return null;
    return this.channels().find(c => c.id === id) ?? null;
  });

  readonly selectedChannelName = computed(() => {
    const ch = this.selectedChannel();
    return ch?.name ?? '';
  });

  readonly isReservedSelected = computed(() => {
    const ch = this.selectedChannel();
    return !!ch?.isReserved;
  });

  openList(): void {
    this.showList.set(true);
  }

  closeList(): void {
    this.showList.set(false);
  }

  onChannelSelected(channel: ChannelDefinition): void {
    this.channelId.set(channel.id);
    this.showList.set(false);
  }
}

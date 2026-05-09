import { Component, computed, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { EnumDefinition } from '../../../../models/enum-definition';
import { EditChannel } from '../edit-channel/edit-channel';

@Component({
  selector: 'lib-channels-list',
  standalone: true,
  imports: [MatIcon, EditChannel],
  templateUrl: './channels-list.html',
  styleUrl: './channels-list.css',
})
export class ChannelsList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly channelDefinitionsChange = output<ChannelDefinition[]>();

  readonly editingChannelId = signal<string | null>(null);

  readonly channels = computed<ChannelDefinition[]>(() => this.configuration()?.channelDefinitions ?? []);
  readonly enumDefinitions = computed<EnumDefinition[]>(() => this.configuration()?.enumDefinitions ?? []);

  readonly hasChannels = computed(() => this.channels().length > 0);

  readonly editingChannel = computed<ChannelDefinition | null>(() => {
    const editingId = this.editingChannelId();
    if (!editingId || editingId === 'new') {
      return null;
    }

    return this.channels().find((channel) => channel.id === editingId) ?? null;
  });

  readonly isEditing = computed(() => this.editingChannelId() !== null);

  startAdd(): void {
    this.editingChannelId.set('new');
  }

  startEdit(channelId: string): void {
    this.editingChannelId.set(channelId);
  }

  stopEdit(): void {
    this.editingChannelId.set(null);
  }

  deleteChannel(channelId: string): void {
    this.channelDefinitionsChange.emit(this.channels().filter((channel) => channel.id !== channelId));

    if (this.editingChannelId() === channelId) {
      this.stopEdit();
    }
  }

  saveChannel(channel: ChannelDefinition): void {
    const channels = this.channels();
    const editedChannelId = this.editingChannelId();

    if (!editedChannelId || editedChannelId === 'new') {
      this.channelDefinitionsChange.emit([...channels, channel]);
      this.stopEdit();
      return;
    }

    const updated = channels.map((existingChannel) =>
      existingChannel.id === editedChannelId ? channel : existingChannel
    );

    this.channelDefinitionsChange.emit(updated);
    this.stopEdit();
  }
}

import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { MatTooltip } from '@angular/material/tooltip';
import { CarConfiguration } from '../../../../models/car-configuration';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { EnumDefinition } from '../../../../models/enum-definition';
import { EditChannel } from '../edit-channel/edit-channel';
import { ChannelUsageService } from '../channel-usage.service';

@Component({
  selector: 'lib-channels-list',
  standalone: true,
  imports: [MatIcon, MatTooltip, EditChannel],
  templateUrl: './channels-list.html',
  styleUrl: './channels-list.css',
})
export class ChannelsList {
  private readonly channelUsageService = inject(ChannelUsageService);

  readonly configuration = input<CarConfiguration | null>(null);
  readonly channelDefinitionsChange = output<ChannelDefinition[]>();

  readonly editingChannelId = signal<string | null>(null);
  readonly deleteBlockedMessage = signal<string | null>(null);

  readonly channels = computed<ChannelDefinition[]>(() =>
    [...(this.configuration()?.channelDefinitions ?? [])].sort((a, b) => a.name.localeCompare(b.name))
  );
  readonly enumDefinitions = computed<EnumDefinition[]>(() => this.configuration()?.enumDefinitions ?? []);

  readonly hasChannels = computed(() => this.channels().length > 0);

  readonly usedChannelIdSet = computed(() => new Set(this.channelUsageService.usedChannelIds()));

  readonly usedChannelCount = computed(() =>
    this.channels().filter(ch => this.usedChannelIdSet().has(ch.id)).length
  );

  readonly unusedChannelCount = computed(() => this.channels().length - this.usedChannelCount());

  /** Map of channelId → usage labels for channels assigned to more than one output source. */
  readonly duplicateUsages = computed<ReadonlyMap<string, string[]>>(() => {
    const result = new Map<string, string[]>();
    for (const [id, labels] of this.channelUsageService.channelUsageMap()) {
      if (labels.length > 1) {
        result.set(id, labels);
      }
    }
    return result;
  });

  readonly channelUsageMap = computed(() => this.channelUsageService.channelUsageMap());

  readonly editingChannel = computed<ChannelDefinition | null>(() => {
    const editingId = this.editingChannelId();
    if (!editingId || editingId === 'new') {
      return null;
    }

    return this.channels().find((channel) => channel.id === editingId) ?? null;
  });

  readonly isEditing = computed(() => this.editingChannelId() !== null);

  private static readonly featureLabels: Readonly<Record<string, string>> = {
    'fuel-analysis': 'Fuel Analysis',
    'throttle-consumption': 'Throttle Consumption',
  };

  isManaged(channel: ChannelDefinition): boolean {
    return channel.managedByFeature !== null && channel.managedByFeature !== '';
  }

  getFeatureLabel(managedByFeature: string | null): string {
    if (!managedByFeature) return '';
    return ChannelsList.featureLabels[managedByFeature]
      ?? managedByFeature.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
  }

  getManagedTooltip(channel: ChannelDefinition): string {
    return `Managed by ${this.getFeatureLabel(channel.managedByFeature)}. Distribution can be edited; disable the feature to remove this channel.`;
  }

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
    const channel = this.channels().find(ch => ch.id === channelId);
    if (channel && this.isManaged(channel)) {
      return;
    }

    const locations = this.channelUsageMap().get(channelId);
    if (locations?.length) {
      const name = channel?.name ?? 'This channel';
      this.deleteBlockedMessage.set(
        `"${name}" is still assigned as an output in the following locations. Unassign it before deleting:\n${locations.map(l => `\u2022 ${l}`).join('\n')}`
      );
      return;
    }

    this.channelDefinitionsChange.emit(this.channels().filter((channel) => channel.id !== channelId));

    if (this.editingChannelId() === channelId) {
      this.stopEdit();
    }
  }

  dismissDeleteError(): void {
    this.deleteBlockedMessage.set(null);
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

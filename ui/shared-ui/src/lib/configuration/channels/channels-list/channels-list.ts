import { Component, computed, input } from '@angular/core';
import { CarConfiguration } from '../../../../models/car-configuration';
import { ChannelDefinition } from '../../../../models/channel-definition';

@Component({
  selector: 'lib-channels-list',
  imports: [],
  templateUrl: './channels-list.html',
  styleUrl: './channels-list.css',
})
export class ChannelsList {
  readonly configuration = input<CarConfiguration | null>(null);

  readonly channels = computed<ChannelDefinition[]>(() => this.configuration()?.channelDefinitions ?? []);

  readonly hasChannels = computed(() => this.channels().length > 0);
}

import { Component, input, model } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { LoggingDefinition } from '../../../../models/logging-definition';
import { LoggingFrequency } from '../../../../models/logging-frequency';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';

@Component({
  selector: 'lib-edit-log-entry',
  standalone: true,
  imports: [ChannelSelector],
  templateUrl: './edit-log-entry.html',
  styleUrl: './edit-log-entry.css',
})
export class EditLogEntry {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly entry = model.required<LoggingDefinition>();

  readonly frequencyOptions: { value: LoggingFrequency; label: string }[] = [
    { value: LoggingFrequency.OncePerSecond, label: '1 Hz' },
    { value: LoggingFrequency.TwicePerSecond, label: '2 Hz' },
    { value: LoggingFrequency.FiveTimesPerSecond, label: '5 Hz' },
    { value: LoggingFrequency.TenTimesPerSecond, label: '10 Hz' },
    { value: LoggingFrequency.TwentyTimesPerSecond, label: '20 Hz' },
  ];

  onChannelChanged(channelId: string | null): void {
    this.entry.set({ ...this.entry(), channelId: channelId ?? '' });
  }

  onFrequencyChanged(value: string): void {
    this.entry.set({ ...this.entry(), frequency: Number(value) as LoggingFrequency });
  }
}

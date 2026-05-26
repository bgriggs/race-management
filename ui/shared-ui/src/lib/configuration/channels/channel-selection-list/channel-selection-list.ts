import { afterNextRender, Component, computed, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ChannelDefinition } from '../../../../models/channel-definition';

@Component({
  selector: 'lib-channel-selection-list',
  imports: [FormsModule, MatSlideToggleModule],
  templateUrl: './channel-selection-list.html',
  styleUrl: './channel-selection-list.css',
})
export class ChannelSelectionList {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);
  readonly channelSelected = output<ChannelDefinition>();

  readonly searchText = signal('');
  readonly showUnusedOnly = signal(true);

  private readonly searchInput = viewChild.required<ElementRef<HTMLInputElement>>('searchInput');

  constructor() {
    afterNextRender(() => this.searchInput().nativeElement.focus());
  }

  readonly filteredChannels = computed(() => {
    let result = this.channels();
    const used = new Set(this.usedChannelIds());

    if (this.showUnusedOnly()) {
      result = result.filter(ch => !used.has(ch.id));
    }

    const search = this.searchText().toLowerCase().trim();
    if (search) {
      result = result.filter(ch =>
        ch.name.toLowerCase().includes(search) ||
        ch.category.toLowerCase().includes(search) ||
        ch.dataType.toLowerCase().includes(search)
      );
    }

    return result;
  });

  isUsed(channelId: string): boolean {
    return this.usedChannelIds().includes(channelId);
  }

  selectChannel(channel: ChannelDefinition): void {
    this.channelSelected.emit(channel);
  }
}

import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { CarConfiguration } from '../../../../../shared-ui/src/models/car-configuration';
import { ChannelDefinition } from '../../../../../shared-ui/src/models/channel-definition';
import { ConfigurationClient } from '../../clients/configuration-client';

interface ChannelRow {
  channel: ChannelDefinition;
  cars: string[];
}

@Component({
  selector: 'app-column-config-dialog',
  templateUrl: './column-config-dialog.html',
  styleUrl: './column-config-dialog.css',
})
export class ColumnConfigDialog {
  private readonly client = inject(ConfigurationClient);

  readonly teamId = input.required<number>();
  readonly cars = input.required<Car[]>();
  readonly excludedChannelIds = input<readonly string[]>([]);
  readonly closed = output<void>();
  readonly selected = output<ChannelDefinition>();

  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly search = signal('');
  private readonly rows = signal<ChannelRow[]>([]);

  protected readonly filteredRows = computed<ChannelRow[]>(() => {
    const q = this.search().trim().toLowerCase();
    const excluded = new Set(this.excludedChannelIds());
    const all = this.rows().filter(r => !excluded.has(r.channel.id));
    return q ? all.filter(r => r.channel.name.toLowerCase().includes(q)) : all;
  });

  constructor() {
    effect(() => {
      const teamId = this.teamId();
      const cars = this.cars();
      void this.loadAll(teamId, cars);
    });
  }

  protected close(): void {
    this.closed.emit();
  }

  protected onSearchInput(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  protected onRowClick(row: ChannelRow): void {
    this.selected.emit(row.channel);
  }

  private async loadAll(teamId: number, cars: Car[]): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    this.rows.set([]);
    try {
      const results = await Promise.all(
        cars.map(async car => {
          try {
            const config = await this.client.loadCarConfigurationByCar(teamId, car.number);
            return { carNumber: car.number, config };
          } catch (err) {
            console.error(`Failed to load car configuration for car ${car.number}`, err);
            return null;
          }
        }),
      );

      const byChannelId = new Map<string, ChannelRow>();
      for (const result of results) {
        if (!result) continue;
        for (const channel of result.config.channelDefinitions ?? []) {
          const existing = byChannelId.get(channel.id);
          if (existing) {
            if (!existing.cars.includes(result.carNumber)) existing.cars.push(result.carNumber);
          } else {
            byChannelId.set(channel.id, { channel, cars: [result.carNumber] });
          }
        }
      }

      for (const row of byChannelId.values()) {
        row.cars.sort(compareCarNumber);
      }

      const all = Array.from(byChannelId.values()).sort((a, b) => {
        if (a.channel.isReserved !== b.channel.isReserved) return a.channel.isReserved ? -1 : 1;
        return a.channel.name.localeCompare(b.channel.name);
      });
      this.rows.set(all);

      if (cars.length > 0 && results.every(r => r === null)) {
        this.loadError.set('Failed to load any car configurations.');
      }
    } finally {
      this.loading.set(false);
    }
  }
}

function compareCarNumber(a: string, b: string): number {
  const na = Number(a);
  const nb = Number(b);
  if (!Number.isNaN(na) && !Number.isNaN(nb)) return na - nb;
  return a.localeCompare(b);
}

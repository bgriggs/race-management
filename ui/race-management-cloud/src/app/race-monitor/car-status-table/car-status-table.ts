import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { ChannelStatusTableColumnConfiguration } from '../../../../../shared-ui/src/cloud-api/channel-status-table-column-configuration';
import { CarConfiguration } from '../../../../../shared-ui/src/models/car-configuration';
import { ChannelDefinition } from '../../../../../shared-ui/src/models/channel-definition';
import { AuthService } from '../../auth.service';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { TelemetryStore } from '../../telemetry/telemetry-store';
import { ColumnConfigDialog } from './column-config-dialog';

interface ConfigEntry {
  definitions: ChannelDefinition[];
  indexByDefinitionId: Map<string, number>;
}

@Component({
  selector: 'app-car-status-table',
  imports: [ColumnConfigDialog],
  templateUrl: './car-status-table.html',
  styleUrl: './car-status-table.css',
})
export class CarStatusTable {
  private readonly client = inject(ConfigurationClient);
  protected readonly teamSelection = inject(TeamSelectionService);
  private readonly auth = inject(AuthService);
  protected readonly telemetry = inject(TelemetryStore);

  protected readonly cars = signal<Car[]>([]);
  protected readonly columns = signal<ChannelStatusTableColumnConfiguration[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly columnConfigOpen = signal(false);

  protected readonly draggingColumnId = signal<string | null>(null);
  protected readonly dropTargetId = signal<string | null>(null);
  protected readonly dropSide = signal<'left' | 'right' | null>(null);
  protected readonly contextMenu = signal<{
    column: ChannelStatusTableColumnConfiguration;
    x: number;
    y: number;
  } | null>(null);

  private readonly configCache = signal<ReadonlyMap<string, ConfigEntry>>(new Map());
  private readonly extraChannelDefs = signal<ReadonlyMap<string, ChannelDefinition>>(new Map());
  private readonly inflightConfigIds = new Set<string>();

  protected readonly columnCount = computed(() => 1 + this.columns().length);
  protected readonly now = signal(Date.now());
  protected readonly existingColumnIds = computed(() =>
    this.columns().map(c => c.channelDefinitionId),
  );

  constructor() {
    const destroyRef = inject(DestroyRef);
    const tick = setInterval(() => this.now.set(Date.now()), 500);
    destroyRef.onDestroy(() => clearInterval(tick));

    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      const userId = this.auth.user()?.email;
      if (teamId === null || !userId) {
        this.cars.set([]);
        this.columns.set([]);
        return;
      }
      void this.load(teamId, userId);
    });

    // Whenever a snapshot surfaces a new configurationId, fetch that config.
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) return;
      const cache = this.configCache();
      for (const car of this.telemetry.carsByKey().values()) {
        const id = car.configurationId;
        if (id && !cache.has(id) && !this.inflightConfigIds.has(id)) {
          void this.fetchConfig(teamId, id);
        }
      }
    });
  }

  protected openColumnConfig(): void {
    this.columnConfigOpen.set(true);
  }

  protected closeColumnConfig(): void {
    this.columnConfigOpen.set(false);
  }

  protected onColumnSelected(channel: ChannelDefinition): void {
    this.columns.update(cols => {
      if (cols.some(c => c.channelDefinitionId === channel.id)) return cols;
      const nextOrder = cols.reduce((max, c) => Math.max(max, c.order), -1) + 1;
      const teamId = this.teamSelection.selectedTeamId() ?? 0;
      const userId = this.auth.user()?.email ?? '';
      return [
        ...cols,
        {
          teamId,
          userId,
          channelDefinitionId: channel.id,
          order: nextOrder,
          nameOverride: null,
        },
      ];
    });
    const nextDefs = new Map(this.extraChannelDefs());
    nextDefs.set(channel.id, channel);
    this.extraChannelDefs.set(nextDefs);
    void this.persistColumns();
  }

  private async persistColumns(): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    const userId = this.auth.user()?.email;
    if (teamId === null || !userId) return;
    const columns = this.columns().map((c, idx) => ({
      teamId,
      userId,
      channelDefinitionId: c.channelDefinitionId,
      order: idx,
      nameOverride: c.nameOverride,
    }));
    try {
      await this.client.saveChannelStatusTableConfiguration(teamId, userId, {
        teamId,
        userId,
        columns,
      });
    } catch (err) {
      console.error('Failed to save channel status table configuration:', err);
    }
  }

  protected onColumnDragStart(col: ChannelStatusTableColumnConfiguration, event: DragEvent): void {
    this.draggingColumnId.set(col.channelDefinitionId);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', col.channelDefinitionId);
    }
  }

  protected onColumnDragOver(col: ChannelStatusTableColumnConfiguration, event: DragEvent): void {
    if (this.draggingColumnId() === null) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const side: 'left' | 'right' = event.clientX > rect.left + rect.width / 2 ? 'right' : 'left';
    this.dropTargetId.set(col.channelDefinitionId);
    this.dropSide.set(side);
  }

  protected onColumnDragLeave(col: ChannelStatusTableColumnConfiguration): void {
    if (this.dropTargetId() === col.channelDefinitionId) {
      this.dropTargetId.set(null);
      this.dropSide.set(null);
    }
  }

  protected onColumnDrop(target: ChannelStatusTableColumnConfiguration, event: DragEvent): void {
    event.preventDefault();
    const sourceId = this.draggingColumnId();
    const side = this.dropSide();
    this.clearDragState();
    if (!sourceId || sourceId === target.channelDefinitionId || !side) return;

    const cols = this.columns();
    const fromIdx = cols.findIndex(c => c.channelDefinitionId === sourceId);
    if (fromIdx < 0) return;

    const next = [...cols];
    const [moved] = next.splice(fromIdx, 1);
    let insertIdx = next.findIndex(c => c.channelDefinitionId === target.channelDefinitionId);
    if (insertIdx < 0) return;
    if (side === 'right') insertIdx++;
    next.splice(insertIdx, 0, moved);
    this.columns.set(next);
    void this.persistColumns();
  }

  protected onColumnDragEnd(): void {
    this.clearDragState();
  }

  protected onColumnContextMenu(col: ChannelStatusTableColumnConfiguration, event: MouseEvent): void {
    event.preventDefault();
    this.contextMenu.set({ column: col, x: event.clientX, y: event.clientY });
  }

  protected closeContextMenu(): void {
    this.contextMenu.set(null);
  }

  protected removeColumn(col: ChannelStatusTableColumnConfiguration): void {
    this.contextMenu.set(null);
    this.columns.update(cols =>
      cols.filter(c => c.channelDefinitionId !== col.channelDefinitionId),
    );
    void this.persistColumns();
  }

  private clearDragState(): void {
    this.draggingColumnId.set(null);
    this.dropTargetId.set(null);
    this.dropSide.set(null);
  }

  protected columnLabel(column: ChannelStatusTableColumnConfiguration): string {
    if (column.nameOverride) return column.nameOverride;
    const def = this.findChannelDefinition(column.channelDefinitionId);
    if (def) return def.name;
    return column.channelDefinitionId.slice(0, 8);
  }

  protected statusColor(car: Car): string {
    const last = this.telemetry.lastTelemetryFor(car.number);
    if (!last) return 'rgb(220, 38, 38)';
    const ageSec = (this.now() - last.getTime()) / 1000;
    if (ageSec <= 0) return 'rgb(34, 197, 94)';
    if (ageSec >= 5) return 'rgb(220, 38, 38)';
    const t = ageSec / 5;
    const r = Math.round(34 + (234 - 34) * t);
    const g = Math.round(197 + (179 - 197) * t);
    const b = Math.round(94 + (8 - 94) * t);
    return `rgb(${r}, ${g}, ${b})`;
  }

  protected statusTitle(car: Car): string {
    const last = this.telemetry.lastTelemetryFor(car.number);
    if (!last) return 'Disconnected';
    return `Last update: ${last.toLocaleString()}`;
  }

  protected channelCellValue(car: Car, column: ChannelStatusTableColumnConfiguration): string {
    const snapshot = this.telemetry.carForNumber(car.number);
    if (!snapshot || !snapshot.configurationId) return '—';
    const entry = this.configCache().get(snapshot.configurationId);
    if (!entry) return '—'; // config still loading
    const sessionIndex = entry.indexByDefinitionId.get(column.channelDefinitionId);
    if (sessionIndex === undefined) return '—'; // channel not in this car's config
    const value = this.channelsAsRecord(snapshot.channels)[sessionIndex];
    return value?.value ?? '—';
  }

  private async load(teamId: number, userId: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [cars, config] = await Promise.all([
        this.client.listCars(teamId),
        this.client.loadChannelStatusTableConfiguration(teamId, userId),
      ]);
      this.cars.set(cars);
      // TODO: when config is null, apply a default column set (e.g. a curated
      // set of reserved channels). Not implemented yet — leave empty for now.
      const columns = config?.columns ?? [];
      this.columns.set([...columns].sort((a, b) => a.order - b.order));
      void this.seedChannelDefinitions(teamId, cars);
    } catch (err) {
      console.error('Failed to load car status table:', err);
      this.error.set('Failed to load car status data.');
    } finally {
      this.loading.set(false);
    }
  }

  private async seedChannelDefinitions(teamId: number, cars: Car[]): Promise<void> {
    const results = await Promise.allSettled(
      cars.map(car => this.client.loadCarConfigurationByCar(teamId, car.number)),
    );
    const nextDefs = new Map(this.extraChannelDefs());
    for (const result of results) {
      if (result.status !== 'fulfilled') continue;
      for (const def of result.value.channelDefinitions ?? []) {
        nextDefs.set(def.id, def);
      }
    }
    this.extraChannelDefs.set(nextDefs);
  }

  private async fetchConfig(teamId: number, configurationId: string): Promise<void> {
    this.inflightConfigIds.add(configurationId);
    try {
      const config = await this.client.loadCarConfigurationById(teamId, configurationId);
      const entry = this.buildConfigEntry(config);
      const next = new Map(this.configCache());
      next.set(configurationId, entry);
      this.configCache.set(next);
    } catch (err) {
      console.error('Failed to load CarConfiguration', configurationId, err);
    } finally {
      this.inflightConfigIds.delete(configurationId);
    }
  }

  private buildConfigEntry(config: CarConfiguration): ConfigEntry {
    const definitions = config.channelDefinitions ?? [];
    const indexByDefinitionId = new Map<string, number>();
    definitions.forEach((def, idx) => indexByDefinitionId.set(def.id, idx));
    return { definitions, indexByDefinitionId };
  }

  private findChannelDefinition(channelDefinitionId: string): ChannelDefinition | undefined {
    const extra = this.extraChannelDefs().get(channelDefinitionId);
    if (extra) return extra;
    for (const entry of this.configCache().values()) {
      const idx = entry.indexByDefinitionId.get(channelDefinitionId);
      if (idx !== undefined) return entry.definitions[idx];
    }
    return undefined;
  }

  private channelsAsRecord(channels: unknown): { [key: number]: { value: string; timestamp: string } } {
    if (channels instanceof Map) {
      const out: { [key: number]: { value: string; timestamp: string } } = {};
      for (const [k, v] of channels) out[Number(k)] = v as { value: string; timestamp: string };
      return out;
    }
    return (channels ?? {}) as { [key: number]: { value: string; timestamp: string } };
  }
}

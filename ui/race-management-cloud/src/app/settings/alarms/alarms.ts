import { Component, computed, effect, inject, signal } from '@angular/core';
import { AlarmDefinitionDto } from '../../../../../shared-ui/src/cloud-api/alarm-definition-dto';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { LogicType } from '../../../../../shared-ui/src/cloud-api/logic-type';
import { ChannelDefinition } from '../../../../../shared-ui/src/models/channel-definition';
import { CarConfiguration } from '../../../../../shared-ui/src/models/car-configuration';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { EditCloudAlarm } from './edit-cloud-alarm';
import { createGuid } from '../../../../../shared-ui/src/lib/utils/guid';

type Filter = 'all' | 'team' | { car: string };

@Component({
  selector: 'app-alarms',
  imports: [EditCloudAlarm],
  templateUrl: './alarms.html',
  styleUrl: './alarms.css',
})
export class Alarms {
  private readonly client = inject(ConfigurationClient);
  protected readonly teamSelection = inject(TeamSelectionService);

  protected readonly alarms = signal<AlarmDefinitionDto[]>([]);
  protected readonly cars = signal<Car[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly filter = signal<Filter>('all');
  protected readonly editingId = signal<string | 'new' | null>(null);
  protected readonly draft = signal<AlarmDefinitionDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  // Per-car channel cache: carNumber → channel definitions from that car's active config.
  // Populated lazily when the editor opens; reused across saves.
  private readonly channelsByCar = new Map<string, ChannelDefinition[]>();
  protected readonly editorChannels = signal<ChannelDefinition[]>([]);
  protected readonly editorChannelsLoading = signal(false);

  protected readonly visibleAlarms = computed(() => {
    const filter = this.filter();
    const alarms = this.alarms();
    if (filter === 'all') return alarms;
    if (filter === 'team') return alarms.filter(a => a.carNumber === null);
    return alarms.filter(a => a.carNumber === filter.car);
  });

  protected readonly isEditing = computed(() => this.editingId() !== null);
  protected readonly editorTitle = computed(() => this.editingId() === 'new' ? 'Add Alarm' : 'Edit Alarm');

  constructor() {
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this.alarms.set([]);
        this.cars.set([]);
        this.channelsByCar.clear();
        return;
      }
      void this.load(teamId);
    });
  }

  protected onFilterChanged(value: string): void {
    if (value === 'all' || value === 'team') {
      this.filter.set(value);
    } else if (value.startsWith('car:')) {
      this.filter.set({ car: value.slice('car:'.length) });
    }
  }

  protected currentFilterValue(): string {
    const f = this.filter();
    if (f === 'all' || f === 'team') return f;
    return `car:${f.car}`;
  }

  protected scopeLabel(alarm: AlarmDefinitionDto): string {
    return alarm.carNumber === null ? 'All cars' : `Car #${alarm.carNumber}`;
  }

  protected statementSummary(alarm: AlarmDefinitionDto): string {
    const groups = alarm.statement?.activateComparisons ?? [];
    const first = groups.find(g => g && g.length > 0);
    if (!first) return '(no conditions)';
    const total = groups.reduce((sum, g) => sum + (g?.length ?? 0), 0);
    const head = first[0];
    const channelName = this.lookupChannelName(head.channelId, alarm.carNumber) ?? '(channel)';
    const op = this.logicLabel(head.logic);
    const rhs = head.useStaticComparison
      ? head.staticValueComparison
      : (this.lookupChannelName(head.channelComparisonId ?? '', alarm.carNumber) ?? '(channel)');
    const base = `${channelName} ${op} ${rhs}`;
    return total > 1 ? `${base}  (+${total - 1} more)` : base;
  }

  protected hasColor(alarm: AlarmDefinitionDto): boolean {
    return !!alarm.displayChannelSourceColorHex && alarm.displayChannelSourceColorHex.length >= 7;
  }

  protected colorSwatch(alarm: AlarmDefinitionDto): string {
    const hex = alarm.displayChannelSourceColorHex || '';
    return hex.length >= 7 ? hex.slice(0, 7) : '#ffffff';
  }

  protected startAdd(): void {
    this.saveError.set(null);
    const filter = this.filter();
    const draft: AlarmDefinitionDto = {
      id: '',
      teamId: this.teamSelection.selectedTeamId() ?? 0,
      carNumber: typeof filter === 'object' ? filter.car : null,
      name: '',
      message: '',
      displayChannelSourceColorHex: '',
      timeAfterAckToDisplaySecs: 60,
      alarmStatusChannelId: null,
      statement: {
        id: createGuid(),
        activateComparisons: [[]],
        deactivateComparisons: null,
      },
    };
    this.draft.set(draft);
    this.editingId.set('new');
    void this.loadEditorChannels(draft.carNumber);
  }

  protected startEdit(alarm: AlarmDefinitionDto): void {
    this.saveError.set(null);
    this.draft.set(structuredClone(alarm));
    this.editingId.set(alarm.id);
    void this.loadEditorChannels(alarm.carNumber);
  }

  protected onDraftChanged(updated: AlarmDefinitionDto): void {
    const current = this.draft();
    if (!current) return;
    this.draft.set(updated);
    // If the user toggled scope/car in the modal, refresh the editor's channel list.
    if (current.carNumber !== updated.carNumber) {
      void this.loadEditorChannels(updated.carNumber);
    }
  }

  protected stopEdit(): void {
    if (this.saving()) return;
    this.editingId.set(null);
    this.draft.set(null);
    this.saveError.set(null);
  }

  protected async saveDraft(): Promise<void> {
    const draft = this.draft();
    const teamId = this.teamSelection.selectedTeamId();
    if (!draft || teamId === null || this.saving()) return;

    this.saving.set(true);
    this.saveError.set(null);
    try {
      await this.client.saveAlarmDefinition(teamId, draft);
      await this.load(teamId);
      this.editingId.set(null);
      this.draft.set(null);
    } catch (err) {
      console.error('Failed to save alarm:', err);
      this.saveError.set('Failed to save alarm. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteAlarm(alarm: AlarmDefinitionDto): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) return;
    if (!confirm(`Delete alarm "${alarm.name}"? This cannot be undone.`)) return;

    this.error.set(null);
    try {
      await this.client.deleteAlarmDefinition(teamId, alarm.id);
      await this.load(teamId);
    } catch (err) {
      console.error('Failed to delete alarm:', err);
      this.error.set('Failed to delete alarm.');
    }
  }

  private async load(teamId: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.channelsByCar.clear();
    try {
      const [alarms, cars] = await Promise.all([
        this.client.loadAlarmDefinitions(teamId),
        this.client.listCars(teamId),
      ]);
      this.alarms.set(alarms);
      this.cars.set(cars);
    } catch (err) {
      console.error('Failed to load alarms:', err);
      this.error.set('Failed to load alarms.');
      this.alarms.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadEditorChannels(carNumber: string | null): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) {
      this.editorChannels.set([]);
      return;
    }

    this.editorChannelsLoading.set(true);
    try {
      if (carNumber !== null) {
        this.editorChannels.set(await this.getChannelsForCar(teamId, carNumber));
        return;
      }

      // Team-level: union of channels across every car on the team, deduped by id.
      const cars = this.cars();
      if (cars.length === 0) {
        this.editorChannels.set([]);
        return;
      }
      const lists = await Promise.all(cars.map(c => this.getChannelsForCar(teamId, c.number)));
      const byId = new Map<string, ChannelDefinition>();
      for (const list of lists) {
        for (const ch of list) {
          if (!byId.has(ch.id)) byId.set(ch.id, ch);
        }
      }
      this.editorChannels.set([...byId.values()].sort((a, b) => a.name.localeCompare(b.name)));
    } catch (err) {
      console.error('Failed to load editor channels:', err);
      this.editorChannels.set([]);
    } finally {
      this.editorChannelsLoading.set(false);
    }
  }

  private async getChannelsForCar(teamId: number, carNumber: string): Promise<ChannelDefinition[]> {
    const cached = this.channelsByCar.get(carNumber);
    if (cached) return cached;
    try {
      const config: CarConfiguration = await this.client.loadCarConfigurationByCar(teamId, carNumber);
      const channels = config.channelDefinitions ?? [];
      this.channelsByCar.set(carNumber, channels);
      return channels;
    } catch (err) {
      // A car may not have an active config yet — treat as empty channel list rather than failing the whole load.
      console.warn(`No active configuration for car ${carNumber}; treating as empty channel list.`, err);
      this.channelsByCar.set(carNumber, []);
      return [];
    }
  }

  private lookupChannelName(channelId: string, carNumber: string | null): string | null {
    if (!channelId) return null;
    if (carNumber !== null) {
      const list = this.channelsByCar.get(carNumber);
      return list?.find(c => c.id === channelId)?.name ?? null;
    }
    for (const list of this.channelsByCar.values()) {
      const found = list.find(c => c.id === channelId);
      if (found) return found.name;
    }
    return null;
  }

  private logicLabel(logic: LogicType): string {
    switch (logic) {
      case LogicType.GreaterThan: return '>';
      case LogicType.LessThan: return '<';
      case LogicType.GreaterThanOrEqualTo: return '≥';
      case LogicType.LessThanOrEqualTo: return '≤';
      case LogicType.EqualTo: return '=';
      case LogicType.Updated: return 'updated';
      case LogicType.ChangedBy: return 'changed by';
      case LogicType.True: return 'always true';
      case LogicType.False: return 'always false';
      default: return String(logic);
    }
  }
}

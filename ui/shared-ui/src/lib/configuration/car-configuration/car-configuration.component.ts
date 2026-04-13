import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ErrorListComponent, ErrorListItem } from '../error-list/error-list.component';
import { NavigationTreeComponent, NavigationTreeNode } from '../navigation-tree/navigation-tree.component';
import {
  MANAGEMENT_DATA_CLIENT,
  type ManagementDataClient
} from '../../data/management-data-client';
import { CarConfigurationSummary } from '../../../models/car-configuration-summary';
import { CarConfiguration } from '../../../models/car-configuration';

@Component({
  selector: 'rm-car-configuration',
  standalone: true,
  imports: [CommonModule, NavigationTreeComponent, ErrorListComponent],
  templateUrl: './car-configuration.component.html',
  styleUrl: './car-configuration.component.css'
})
export class CarConfigurationComponent implements OnInit {
  private readonly managementDataClient = inject(MANAGEMENT_DATA_CLIENT);

  readonly canBusEnabled = signal(true);
  readonly selectedNodeId = signal('general-settings');
  readonly loadingSummaries = signal(false);
  readonly summaryLoadError = signal<string | null>(null);
  readonly activeConfiguration = signal<CarConfiguration | null>(null);
  readonly configurationSummaries = signal<CarConfigurationSummary[]>([]);

  readonly treeNodes = computed<NavigationTreeNode[]>(() => this.buildTreeNodes());

  readonly validationErrors: ErrorListItem[] = [
    {
      id: 'general-name-required',
      nodeId: 'general-settings',
      pageLabel: 'General Settings',
      message: 'Car display name is required.'
    },
    {
      id: 'can-bus-bitrate-required',
      nodeId: 'can-bus',
      pageLabel: 'CAN Bus',
      message: 'CAN Bus bitrate must be selected.'
    }
  ];

  readonly errorNodeIds = computed(() => new Set(this.validationErrors.map((item) => item.nodeId)));

  readonly selectedPageLabel = computed(() => this.pageMeta[this.selectedNodeId()]?.label ?? 'Configuration');

  readonly selectedPageDescription = computed(() => this.pageMeta[this.selectedNodeId()]?.description ?? 'This configuration page will be implemented next.');

  readonly configurationSummaryCount = computed(() => this.configurationSummaries().length);

  private readonly pageMeta: Record<string, { label: string; description: string }> = {
    'general-settings': {
      label: 'General Settings',
      description: 'General configuration form content will be added here.'
    },
    channels: {
      label: 'Channels',
      description: 'Channel configuration editor will be added here.'
    },
    communications: {
      label: 'Communications',
      description: 'Communication settings will be added here.'
    },
    'can-bus': {
      label: 'CAN Bus',
      description: 'CAN Bus configuration content will be added here.'
    },
    alarms: {
      label: 'Alarms',
      description: 'Alarm rules editor will be added here.'
    },
    logging: {
      label: 'Logging',
      description: 'Logging configuration content will be added here.'
    },
    tables: {
      label: 'Tables',
      description: 'Table interpolation configuration will be added here.'
    },
    timers: {
      label: 'Timers',
      description: 'Timer configuration content will be added here.'
    },
    math: {
      label: 'Math',
      description: 'Math channel configuration will be added here.'
    },
    'user-conditions': {
      label: 'User Conditions',
      description: 'User condition rules configuration will be added here.'
    },
    counters: {
      label: 'Counters',
      description: 'Counter configuration content will be added here.'
    }
  };

  selectNode(nodeId: string): void {
    this.selectedNodeId.set(nodeId);
  }

  async ngOnInit(): Promise<void> {
    this.loadingSummaries.set(true);
    this.summaryLoadError.set(null);

    try {
      this.configurationSummaries.set(await this.managementDataClient.loadCarConfigurationSummariesAsync());
    } catch {
      this.summaryLoadError.set('Unable to load saved configurations.');
    } finally {
      this.loadingSummaries.set(false);
    }
  }

  async openConfiguration(configurationId: string): Promise<void> {
    this.loadingSummaries.set(true);
    this.summaryLoadError.set(null);

    try {
      this.activeConfiguration.set(await this.managementDataClient.loadCarConfigurationAsync(
        configurationId
      ));
    } catch {
      this.summaryLoadError.set('Unable to load configuration details.');
    } finally {
      this.loadingSummaries.set(false);
    }
  }

  createNewConfiguration(): void {
    this.activeConfiguration.set(this.buildEmptyConfiguration());
    this.summaryLoadError.set(null);
  }

  backToConfigurationPicker(): void {
    this.activeConfiguration.set(null);
    this.selectedNodeId.set('general-settings');
  }

  navigateFromError(nodeId: string): void {
    if (nodeId === 'can-bus' && !this.canBusEnabled()) {
      this.canBusEnabled.set(true);
    }

    this.selectNode(nodeId);
  }

  onCanBusFeatureToggle(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.canBusEnabled.set(target.checked);

    if (!this.canBusEnabled() && this.selectedNodeId() === 'can-bus') {
      this.selectedNodeId.set('communications');
    }
  }

  private buildTreeNodes(): NavigationTreeNode[] {
    return [
      { id: 'general-settings', label: 'General Settings' },
      { id: 'channels', label: 'Channels' },
      {
        id: 'communications',
        label: 'Communications',
        children: [
          {
            id: 'can-bus',
            label: 'CAN Bus',
            visible: this.canBusEnabled()
          }
        ]
      },
      { id: 'alarms', label: 'Alarms' },
      { id: 'logging', label: 'Logging' },
      { id: 'tables', label: 'Tables' },
      { id: 'timers', label: 'Timers' },
      { id: 'math', label: 'Math' },
      { id: 'user-conditions', label: 'User Conditions' },
      { id: 'counters', label: 'Counters' }
    ];
  }

  private buildEmptyConfiguration(): CarConfiguration {
    return {
      configurationId: crypto.randomUUID(),
      configurationSchemaVersion: 1,
      name: '',
      notes: '',
      lastUpdated: new Date(),
      car: '',
      clientId: '',
      clientSecret: '',
      canConfig: {
        isEnabled: false,
        canId: 0,
        canBusId: 0,
        isExtended: false,
        length: 8,
        isBigEndian: true,
        isReceive: true,
        transmitRate: '00:00:01',
        channelAssignments: []
      },
      channelDefinitions: [],
      counterDefinitions: [],
      mathDefinitions: [],
      tableMappings: [],
      timerDefinitions: [],
      userConditions: []
    };
  }
}

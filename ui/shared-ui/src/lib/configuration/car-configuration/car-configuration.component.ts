import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ErrorListComponent, ErrorListItem } from '../error-list/error-list.component';
import { NavigationTreeComponent, NavigationTreeNode } from '../navigation-tree/navigation-tree.component';
import { GeneralSettings } from '../general-settings/general-settings';
import { CommunicationsSettings } from '../communications-settings/communications-settings';
import { CloudConfiguration } from '../cloud-configuration/cloud-configuration';
import { ChannelsList } from '../channels/channels-list/channels-list';
import { CanBusConfig } from '../can-bus/can-bus-config/can-bus-config';
import { CanBusTable } from '../can-bus/can-bus-table/can-bus-table';
import { MatIcon } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData
} from '../../dialogs/confirm-dialog/confirm-dialog.component';
import {
  MANAGEMENT_DATA_CLIENT,
  type ManagementDataClient
} from '../../data/management-data-client';
import { CarConfigurationSummary } from '../../../models/car-configuration-summary';
import { CarConfiguration } from '../../../models/car-configuration';
import { ChannelDefinition } from '../../../models/channel-definition';
import { CanBusConfig as CanBusConfigModel } from '../../../models/can-bus-config';
import { CanBusInterfaceConfig } from '../../../models/can-bus-interface-config';

@Component({
  selector: 'rm-car-configuration',
  standalone: true,
  imports: [
    CommonModule,
    NavigationTreeComponent,
    ErrorListComponent,
    GeneralSettings,
    CommunicationsSettings,
    CloudConfiguration,
    ChannelsList,
    CanBusConfig,
    CanBusTable,
    MatIcon
  ],
  templateUrl: './car-configuration.component.html',
  styleUrl: './car-configuration.component.css'
})
export class CarConfigurationComponent implements OnInit {
  private readonly managementDataClient = inject(MANAGEMENT_DATA_CLIENT);
  private readonly dialog = inject(MatDialog);
  private snackbarTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private readonly emptyCanBusConfig: CanBusConfigModel = { canBusEnabled: [], interfaces: [] };

  readonly canBusEnabled = signal(true);
  readonly selectedNodeId = signal('general-settings');
  readonly loadingSummaries = signal(false);
  readonly summaryLoadError = signal<string | null>(null);
  readonly snackbarMessage = signal<string | null>(null);
  readonly errorDialogMessage = signal<string | null>(null);
  readonly activeConfiguration = signal<CarConfiguration | null>(null);
  readonly configurationSummaries = signal<CarConfigurationSummary[]>([]);

  readonly treeNodes = computed<NavigationTreeNode[]>(() => this.buildTreeNodes());

  readonly canBusConfig = computed<CanBusConfigModel>(() =>
    this.normalizeCanBusConfig(this.activeConfiguration()?.canConfig ?? this.emptyCanBusConfig)
  );

  readonly canBus1Enabled = computed(() => this.canBusConfig().canBusEnabled[0] ?? false);

  readonly canBus2Enabled = computed(() => this.canBusConfig().canBusEnabled[1] ?? false);

  readonly canBus1Config = computed(() => this.canBusConfig().interfaces[0] ?? this.buildEmptyCanBusInterfaceConfig('can0'));

  readonly canBus2Config = computed(() => this.canBusConfig().interfaces[1] ?? this.buildEmptyCanBusInterfaceConfig('can1'));

  readonly cloudConnectionEnabled = computed(
    () => this.activeConfiguration()?.isCloudConnectionEnabled ?? false
  );

  readonly cloudCredentials = computed<Pick<CarConfiguration, 'clientId' | 'clientSecret'> | null>(() => {
    const configuration = this.activeConfiguration();
    if (!configuration) {
      return null;
    }

    return {
      clientId: configuration.clientId,
      clientSecret: configuration.clientSecret
    };
  });

  readonly validationErrors = computed<ErrorListItem[]>(() => {
    const config = this.activeConfiguration();
    if (!config) {
      return [];
    }

    const errors: ErrorListItem[] = [];
    const trimmedName = config.name.trim();

    if (trimmedName.length === 0) {
      errors.push({
        id: 'general-name-required',
        nodeId: 'general-settings',
        pageLabel: 'General Settings',
        message: 'Car display name is required.'
      });
    }

    const trimmedCar = config.car.trim();
    if (trimmedCar.length === 0 || trimmedCar.length > 6) {
      errors.push({
        id: 'general-car-invalid',
        nodeId: 'general-settings',
        pageLabel: 'General Settings',
        message: 'Car is required and must be 1 to 6 characters.'
      });
    }

    if (this.cloudConnectionEnabled()) {
      const trimmedClientId = config.clientId.trim();
      if (trimmedClientId.length === 0 || trimmedClientId.length > 64) {
        errors.push({
          id: 'cloud-client-id-invalid',
          nodeId: 'cloud-configuration',
          pageLabel: 'Cloud Configuration',
          message: 'Client ID is required and must be 64 characters or fewer.'
        });
      }

      const trimmedClientSecret = config.clientSecret.trim();
      if (trimmedClientSecret.length === 0 || trimmedClientSecret.length > 32) {
        errors.push({
          id: 'cloud-client-secret-invalid',
          nodeId: 'cloud-configuration',
          pageLabel: 'Cloud Configuration',
          message: 'Client Secret is required and must be 32 characters or fewer.'
        });
      }
    }

    return errors;
  });

  readonly canSubmitConfiguration = computed(
    () => !!this.activeConfiguration() && this.validationErrors().length === 0
  );

  readonly errorNodeIds = computed(() => new Set(this.validationErrors().map((item) => item.nodeId)));

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
      description: 'Review channels configured for this car.'
    },
    communications: {
      label: 'Communications',
      description: 'Communication settings will be added here.'
    },
    'cloud-configuration': {
      label: 'Cloud Configuration',
      description: 'Cloud communication credentials and endpoint settings are configured here.'
    },
    'can-bus': {
      label: 'CAN Bus',
      description: 'Enable and configure CAN Bus interfaces.'
    },
    'can-bus-1': {
      label: 'CAN 1',
      description: 'Configure CAN Bus interface 1 messages and channel assignments.'
    },
    'can-bus-2': {
      label: 'CAN 2',
      description: 'Configure CAN Bus interface 2 messages and channel assignments.'
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
      const loaded = await this.managementDataClient.loadCarConfigurationAsync(configurationId);
      this.activeConfiguration.set(this.normalizeConfiguration(loaded));
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

  async deleteConfiguration(configurationId: string): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
          data: {
            title: 'Delete Configuration',
            message: 'Are you sure you want to delete this configuration? This action cannot be undone.',
            confirmLabel: 'Delete',
            cancelLabel: 'Cancel',
          },
          width: '400px',
        })
        .afterClosed()
    );

    if (!confirmed) return;

    try {
      await this.managementDataClient.deleteCarConfigurationAsync(configurationId);
      this.configurationSummaries.set(await this.managementDataClient.loadCarConfigurationSummariesAsync());
      this.summaryLoadError.set(null);
      this.showSnackbar('Configuration deleted.');
    } catch (error: unknown) {
      this.openErrorDialog(this.getErrorMessage(error, 'Unable to delete configuration.'));
    }
  }

  async saveConfiguration(): Promise<void> {
    const current = this.activeConfiguration();
    if (!current) return;

    if (!this.canSubmitConfiguration()) {
      this.openErrorDialog('Please resolve validation errors before saving.');
      return;
    }

    try {
      const saved = await this.managementDataClient.saveCarConfigurationAsync(current);
      this.activeConfiguration.set(this.normalizeConfiguration(saved));
      this.configurationSummaries.set(await this.managementDataClient.loadCarConfigurationSummariesAsync());
      this.summaryLoadError.set(null);
      this.showSnackbar('Configuration saved.');
    } catch (error: unknown) {
      this.openErrorDialog(this.getErrorMessage(error, 'Unable to save configuration.'));
    }
  }

  async transmitToCar(): Promise<void> {
    const current = this.activeConfiguration();
    if (!current) return;

    if (!this.canSubmitConfiguration()) {
      this.openErrorDialog('Please resolve validation errors before transmitting to car.');
      return;
    }

    try {
      const updated = await this.managementDataClient.transmitToCarAsync(current);
      this.activeConfiguration.set(this.normalizeConfiguration(updated));
      this.configurationSummaries.set(await this.managementDataClient.loadCarConfigurationSummariesAsync());
      this.summaryLoadError.set(null);
      this.showSnackbar('Configuration transmitted to car.');
    } catch (error: unknown) {
      this.openErrorDialog(this.getErrorMessage(error, 'Unable to transmit configuration to car.'));
    }
  }

  closeErrorDialog(): void {
    this.errorDialogMessage.set(null);
  }

  backToConfigurationPicker(): void {
    this.activeConfiguration.set(null);
    this.selectedNodeId.set('general-settings');
  }

  onGeneralSettingsChange(data: Pick<CarConfiguration, 'name' | 'car' | 'notes'>): void {
    const current = this.activeConfiguration();
    if (!current) return;
    this.activeConfiguration.set({ ...current, ...data });
  }

  onChannelDefinitionsChange(channelDefinitions: ChannelDefinition[]): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      channelDefinitions
    });
  }

  navigateFromError(nodeId: string): void {
    if (nodeId === 'can-bus' && !this.canBusEnabled()) {
      this.canBusEnabled.set(true);
    }
    if (nodeId === 'can-bus-1' && !this.canBus1Enabled()) {
      this.canBusEnabled.set(true);
      this.updateCanBusEnabled(0, true);
    }
    if (nodeId === 'can-bus-2' && !this.canBus2Enabled()) {
      this.canBusEnabled.set(true);
      this.updateCanBusEnabled(1, true);
    }

    this.selectNode(nodeId);
  }

  onCanBusFeatureToggle(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.onCanBusEnabledChange(target.checked);
  }

  onCanBusEnabledChange(enabled: boolean): void {
    this.canBusEnabled.set(enabled);

    if (!enabled) {
      const selectedId = this.selectedNodeId();
      if (selectedId === 'can-bus' || selectedId === 'can-bus-1' || selectedId === 'can-bus-2') {
        this.selectedNodeId.set('communications');
      }
    }
  }

  onCanBus1EnabledChange(enabled: boolean): void {
    this.updateCanBusEnabled(0, enabled);

    if (!enabled && this.selectedNodeId() === 'can-bus-1') {
      this.selectedNodeId.set('can-bus');
    }
  }

  onCanBus2EnabledChange(enabled: boolean): void {
    this.updateCanBusEnabled(1, enabled);

    if (!enabled && this.selectedNodeId() === 'can-bus-2') {
      this.selectedNodeId.set('can-bus');
    }
  }

  onCanBus1ConfigChange(config: CanBusInterfaceConfig): void {
    this.updateCanBusInterface(0, config);
  }

  onCanBus2ConfigChange(config: CanBusInterfaceConfig): void {
    this.updateCanBusInterface(1, config);
  }

  onCloudConnectionEnabledChange(enabled: boolean): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      isCloudConnectionEnabled: enabled
    });

    if (!enabled && this.selectedNodeId() === 'cloud-configuration') {
      this.selectedNodeId.set('communications');
    }
  }

  onCloudConfigurationChange(data: Pick<CarConfiguration, 'clientId' | 'clientSecret'>): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      ...data
    });
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
            id: 'cloud-configuration',
            label: 'Cloud Configuration',
            visible: this.cloudConnectionEnabled()
          },
          {
            id: 'can-bus',
            label: 'CAN Bus',
            visible: this.canBusEnabled(),
            children: [
              { id: 'can-bus-1', label: 'CAN 1', visible: this.canBus1Enabled() },
              { id: 'can-bus-2', label: 'CAN 2', visible: this.canBus2Enabled() }
            ]
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

  private showSnackbar(message: string): void {
    this.snackbarMessage.set(message);

    if (this.snackbarTimeoutId) {
      clearTimeout(this.snackbarTimeoutId);
    }

    this.snackbarTimeoutId = setTimeout(() => {
      this.snackbarMessage.set(null);
      this.snackbarTimeoutId = null;
    }, 3000);
  }

  private openErrorDialog(message: string): void {
    this.errorDialogMessage.set(message);
  }

  private getErrorMessage(error: unknown, fallbackMessage: string): string {
    if (error instanceof Error && error.message.trim()) {
      return `${fallbackMessage} ${error.message}`;
    }

    return fallbackMessage;
  }

  private buildEmptyCanBusInterfaceConfig(interfaceName: string): CanBusInterfaceConfig {
    return {
      interfaceName,
      bitRate: 1000000,
      silentOnCanBus: false,
      messages: [],
    };
  }

  private normalizeCanBusConfig(config: CanBusConfigModel): CanBusConfigModel {
    const interfaces = [...config.interfaces];
    const enabled = [...config.canBusEnabled];

    while (interfaces.length < 2) {
      interfaces.push(this.buildEmptyCanBusInterfaceConfig(`can${interfaces.length}`));
    }

    while (enabled.length < 2) {
      enabled.push(false);
    }

    return {
      canBusEnabled: enabled.slice(0, 2),
      interfaces: interfaces.slice(0, 2)
    };
  }

  private updateCanBusEnabled(index: number, enabled: boolean): void {
    const currentConfiguration = this.activeConfiguration();
    if (!currentConfiguration) {
      return;
    }

    const current = this.normalizeCanBusConfig(currentConfiguration.canConfig);
    const canBusEnabled = [...current.canBusEnabled];
    canBusEnabled[index] = enabled;

    this.activeConfiguration.set({
      ...currentConfiguration,
      canConfig: {
        ...current,
        canBusEnabled
      }
    });
  }

  private updateCanBusInterface(index: number, config: CanBusInterfaceConfig): void {
    const currentConfiguration = this.activeConfiguration();
    if (!currentConfiguration) {
      return;
    }

    const current = this.normalizeCanBusConfig(currentConfiguration.canConfig);
    const interfaces = [...current.interfaces];
    interfaces[index] = config;

    this.activeConfiguration.set({
      ...currentConfiguration,
      canConfig: {
        ...current,
        interfaces
      }
    });
  }

  private normalizeConfiguration(configuration: CarConfiguration): CarConfiguration {
    return {
      ...configuration,
      canConfig: this.normalizeCanBusConfig(configuration.canConfig)
    };
  }

  private buildEmptyConfiguration(): CarConfiguration {
    return {
      configurationId: '00000000-0000-0000-0000-000000000000',
      configurationSchemaVersion: 1,
      name: '',
      notes: '',
      lastUpdated: new Date('0001-01-01T00:00:00.000Z'),
      lastUpdatedOnCarTimestamp: null,
      car: '',
      isCloudConnectionEnabled: false,
      clientId: '',
      clientSecret: '',
      canConfig: this.normalizeCanBusConfig(this.emptyCanBusConfig),
      channelDefinitions: [],
      counterDefinitions: [],
      mathDefinitions: [],
      tableMappings: [],
      timerDefinitions: [],
      userConditions: []
    };
  }
}

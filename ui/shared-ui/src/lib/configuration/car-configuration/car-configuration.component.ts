import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ErrorListComponent, ErrorListItem } from '../error-list/error-list.component';
import { NavigationTreeComponent, NavigationTreeNode } from '../navigation-tree/navigation-tree.component';
import { GeneralSettings } from '../general-settings/general-settings';
import { CommunicationsSettings } from '../communications-settings/communications-settings';
import { CloudConfiguration } from '../cloud-configuration/cloud-configuration';
import { ChannelsList } from '../channels/channels-list/channels-list';
import { CanBusConfig } from '../can-bus/can-bus-config/can-bus-config';
import { CanBusTable } from '../can-bus/can-bus-table/can-bus-table';
import { CounterList } from '../counters/counter-list/counter-list';
import { TimersList } from '../timers/timers-list/timers-list';
import { UserConditionsList } from '../user-conditions/user-conditions-list/user-conditions-list';
import { AlarmList } from '../alarms/alarm-list/alarm-list';
import { MathList } from '../math/math-list/math-list';
import { EnumList } from '../enums/enum-list/enum-list';
import { LogList } from '../logging/log-list/log-list';
import { TableList } from '../tables/table-list/table-list';
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
import { ChannelUsageService } from '../channels/channel-usage.service';
import { CarFuelConfig } from '../../../models/car-fuel-config';
import { ThrottleConsumptionConfig as ThrottleConsumptionConfigModel } from '../../../models/throttle-consumption-config';
import { FuelConfiguration } from './fuel-analysis/fuel-configuration/fuel-configuration';
import { ThrottleConsumptionConfig } from './fuel-analysis/throttle-consumption-config/throttle-consumption-config';

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
    CounterList,
    TimersList,
    UserConditionsList,
    AlarmList,
    MathList,
    EnumList,
    LogList,
    TableList,
    FuelConfiguration,
    ThrottleConsumptionConfig,
    MatIcon
  ],
  templateUrl: './car-configuration.component.html',
  styleUrl: './car-configuration.component.css'
})
export class CarConfigurationComponent implements OnInit {
  private readonly managementDataClient = inject(MANAGEMENT_DATA_CLIENT);
  private readonly dialog = inject(MatDialog);
  private readonly channelUsageService = inject(ChannelUsageService);
  private snackbarTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private readonly emptyCanBusConfig: CanBusConfigModel = { canBusEnabled: [], interfaces: [] };

  private readonly _syncUsedChannelIds = effect(() => {
    this.channelUsageService.updateFromConfiguration(this.activeConfiguration());
  });

  /** Channel IDs currently assigned as outputs anywhere in the configuration — passed to channel pickers so they can dim/hide already-used choices. */
  readonly usedChannelIds = this.channelUsageService.usedChannelIds;

  readonly canBusEnabled = signal(true);
  readonly selectedNodeId = signal('general-settings');
  readonly loadingSummaries = signal(false);
  readonly summaryLoadError = signal<string | null>(null);
  readonly snackbarMessage = signal<string | null>(null);
  readonly errorDialogMessage = signal<string | null>(null);
  readonly duplicateAssignmentError = signal<Array<{ channelName: string; locations: string[] }> | null>(null);
  readonly activeConfiguration = signal<CarConfiguration | null>(null);
  readonly configurationSummaries = signal<CarConfigurationSummary[]>([]);
  readonly reservedChannels = signal<ChannelDefinition[]>([]);

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

  readonly fuelConfig = computed<CarFuelConfig | null>(
    () => this.activeConfiguration()?.fuelConfig ?? null
  );

  readonly fuelAnalysisEnabled = computed(
    () => this.activeConfiguration()?.fuelConfig?.isEnabled ?? false
  );

  readonly throttleConsumptionEnabled = computed(
    () => this.activeConfiguration()?.fuelConfig?.throttleConsumption?.isEnabled ?? false
  );

  readonly throttleConsumption = computed<ThrottleConsumptionConfigModel | null>(
    () => this.activeConfiguration()?.fuelConfig?.throttleConsumption ?? null
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
    },
    enumerations: {
      label: 'Enumerations',
      description: 'Define enumerations that can change a numeric value to text. For example, gear can be converted from a value of 0 to N, etc.'
    },
    'fuel-analysis': {
      label: 'Fuel Analysis',
      description: 'Enable and configure the fuel range analysis for this car.'
    },
    'throttle-consumption': {
      label: 'Throttle Consumption',
      description: 'Configure the in-car throttle-position consumption proxy.'
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

    try {
      this.reservedChannels.set(await this.managementDataClient.loadReservedChannelDefinitionsAsync());
    } catch {
      // Reserved-channel load failure is non-fatal: feature-toggle auto-injection
      // will be a no-op until a successful reload happens.
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

    const duplicates = this.buildDuplicateAssignmentErrors();
    if (duplicates) {
      this.duplicateAssignmentError.set(duplicates);
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

    const duplicates = this.buildDuplicateAssignmentErrors();
    if (duplicates) {
      this.duplicateAssignmentError.set(duplicates);
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

  closeDuplicateAssignmentError(): void {
    this.duplicateAssignmentError.set(null);
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

  onUserConditionsChange(userConditions: CarConfiguration['userConditions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      userConditions
    });
  }

  onTimerDefinitionsChange(timerDefinitions: CarConfiguration['timerDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      timerDefinitions
    });
  }

  onCounterDefinitionsChange(counterDefinitions: CarConfiguration['counterDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      counterDefinitions
    });
  }

  onMathDefinitionsChange(mathDefinitions: CarConfiguration['mathDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      mathDefinitions
    });
  }

  onAlarmDefinitionsChange(alarmDefinitions: CarConfiguration['alarmDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      alarmDefinitions
    });
  }

  onLoggingDefinitionsChange(loggingDefinitions: CarConfiguration['loggingDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      loggingDefinitions
    });
  }

  onEnumDefinitionsChange(enumDefinitions: CarConfiguration['enumDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      enumDefinitions
    });
  }

  onTableDefinitionsChange(tableDefinitions: CarConfiguration['tableDefinitions']): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      tableDefinitions
    });
  }

  onFuelConfigChange(fuelConfig: CarFuelConfig): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    const previousFuelEnabled = current.fuelConfig?.isEnabled ?? false;
    const previousThrottleEnabled = current.fuelConfig?.throttleConsumption?.isEnabled ?? false;

    // Cascade: disabling fuel-analysis also disables throttle-consumption.
    const cascadedConfig: CarFuelConfig = !fuelConfig.isEnabled && fuelConfig.throttleConsumption.isEnabled
      ? { ...fuelConfig, throttleConsumption: { ...fuelConfig.throttleConsumption, isEnabled: false } }
      : fuelConfig;

    const newFuelEnabled = cascadedConfig.isEnabled;
    const newThrottleEnabled = cascadedConfig.throttleConsumption.isEnabled;

    let channelDefinitions = current.channelDefinitions;
    if (!previousFuelEnabled && newFuelEnabled) {
      channelDefinitions = this.addManagedChannels(channelDefinitions, 'fuel-analysis');
    } else if (previousFuelEnabled && !newFuelEnabled) {
      channelDefinitions = this.removeManagedChannels(channelDefinitions, 'fuel-analysis');
    }
    if (!previousThrottleEnabled && newThrottleEnabled) {
      channelDefinitions = this.addManagedChannels(channelDefinitions, 'throttle-consumption');
    } else if (previousThrottleEnabled && !newThrottleEnabled) {
      channelDefinitions = this.removeManagedChannels(channelDefinitions, 'throttle-consumption');
    }

    this.activeConfiguration.set({
      ...current,
      fuelConfig: cascadedConfig,
      channelDefinitions
    });

    if (!newThrottleEnabled && this.selectedNodeId() === 'throttle-consumption') {
      this.selectedNodeId.set('fuel-analysis');
    }
  }

  private addManagedChannels(existing: ChannelDefinition[], featureName: string): ChannelDefinition[] {
    const managed = this.reservedChannels().filter((channel) => channel.managedByFeature === featureName);
    if (managed.length === 0) {
      return existing;
    }
    const existingIds = new Set(existing.map((channel) => channel.id));
    const toAdd = managed.filter((channel) => !existingIds.has(channel.id));
    return toAdd.length === 0 ? existing : [...existing, ...toAdd];
  }

  private removeManagedChannels(existing: ChannelDefinition[], featureName: string): ChannelDefinition[] {
    return existing.filter((channel) => channel.managedByFeature !== featureName);
  }

  onThrottleConsumptionChange(throttleConsumption: ThrottleConsumptionConfigModel): void {
    const current = this.activeConfiguration();
    if (!current) {
      return;
    }

    this.activeConfiguration.set({
      ...current,
      fuelConfig: {
        ...current.fuelConfig,
        throttleConsumption
      }
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
      { id: 'counters', label: 'Counters' },
      { id: 'enumerations', label: 'Enumerations' },
      {
        id: 'fuel-analysis',
        label: 'Fuel Analysis',
        children: [
          {
            id: 'throttle-consumption',
            label: 'Throttle Consumption',
            visible: this.fuelAnalysisEnabled() && this.throttleConsumptionEnabled()
          }
        ]
      }
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

  private buildDuplicateAssignmentErrors(): Array<{ channelName: string; locations: string[] }> | null {
    const config = this.activeConfiguration();
    if (!config) return null;

    const channelNameById = new Map(config.channelDefinitions.map(ch => [ch.id, ch.name]));
    const duplicates: Array<{ channelName: string; locations: string[] }> = [];

    for (const [channelId, locations] of this.channelUsageService.channelUsageMap()) {
      if (locations.length > 1) {
        duplicates.push({
          channelName: channelNameById.get(channelId) ?? channelId,
          locations
        });
      }
    }

    return duplicates.length > 0 ? duplicates : null;
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
      alarmDefinitions: [],
      counterDefinitions: [],
      mathDefinitions: [],
      tableDefinitions: [],
      timerDefinitions: [],
      userConditions: [],
      loggingDefinitions: [],
      enumDefinitions: [],
      fuelConfig: {
        isEnabled: false,
        tankCapacityGallons: 0,
        defaultConsumptionGalPerMin: 0,
        defaultYellowConsumptionMultiplier: 0.5,
        defaultCode35ConsumptionMultiplier: 0.3,
        tripFuelChannelId: 'acd3d127-acaf-4f8a-b27a-8623cfda09f3',
        fuelUsedChannelId: '740ce2a6-dc88-4425-85dc-7f99f2a902f1',
        fuelFullChannelId: 'c3b94831-95f6-4935-bf67-1aacfd611f75',
        inPitChannelId: 'da12563a-1167-4899-9956-700b0b693005',
        throttleConsumption: {
          isEnabled: false,
          maxRpm: 0,
          throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
          engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
        }
      }
    };
  }
}

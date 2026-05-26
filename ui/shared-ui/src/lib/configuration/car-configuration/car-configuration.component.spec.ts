import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { CarConfigurationComponent } from './car-configuration.component';
import {
  MANAGEMENT_DATA_CLIENT,
  type ManagementDataClient
} from '../../data/management-data-client';
import type { CarConfiguration } from '../../../models/car-configuration';
import type { ChannelDefinition } from '../../../models/channel-definition';
import { ChannelDistribution } from '../../../models/channel-distribution';
import { ChannelScope } from '../../../models/channel-scope';

try {
  getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Test environment may already be initialized by the runner.
}

const DEFAULT_THROTTLE_CHANNEL_IDS = {
  throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
  engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
};

const DEFAULT_FUEL_CHANNEL_IDS = {
  fuelLevelChannelId: 'a2529acf-a7c6-449f-8a85-c7d76b35dbcb',
  tripFuelChannelId: 'acd3d127-acaf-4f8a-b27a-8623cfda09f3',
  fuelUsedChannelId: '740ce2a6-dc88-4425-85dc-7f99f2a902f1',
  fuelFullChannelId: 'c3b94831-95f6-4935-bf67-1aacfd611f75',
  inPitChannelId: 'da12563a-1167-4899-9956-700b0b693005',
};

function buildConfig(name: string): CarConfiguration {
  return {
    configurationId: 'cfg-1',
    configurationSchemaVersion: 1,
    name,
    notes: 'notes',
    lastUpdated: new Date('2026-01-01T00:00:00Z'),
    lastUpdatedOnCarTimestamp: null,
    car: 'car-1',
    isCloudConnectionEnabled: true,
    clientId: 'client-id',
    clientSecret: 'client-secret',
    canConfig: {
      canBusEnabled: [false, false],
      interfaces: [
        {
          interfaceName: 'can0',
          bitRate: 1000000,
          silentOnCanBus: false,
          messages: []
        },
        {
          interfaceName: 'can1',
          bitRate: 1000000,
          silentOnCanBus: false,
          messages: []
        }
      ]
    },
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
      ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: false, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
    }
  };
}

function buildReservedChannel(
  id: string,
  name: string,
  managedByFeature: string | null,
  distribution: ChannelDistribution = ChannelDistribution.CarToCloud
): ChannelDefinition {
  return {
    id,
    isReserved: true,
    category: 'Fuel',
    name,
    abbreviation: name.slice(0, 6).toUpperCase(),
    dataType: 'Unitless',
    baseUnitType: '',
    outputUnitType: '',
    outputDecimalPlaces: 0,
    lowRange: 0,
    highRange: 100,
    defaultValue: 0,
    groupTag: '',
    enumConversion: null,
    timeoutMs: 3000,
    distribution,
    isDistributionLocked: false,
    scope: ChannelScope.PerCar,
    managedByFeature,
    producedByFeature: null
  };
}

describe('CarConfigurationComponent', () => {
  let mockClient: ManagementDataClient;

  beforeEach(async () => {
    mockClient = {
      listDiscoveredRacecarsAsync: vi.fn().mockResolvedValue([]),
      getActiveRacecarAsync: vi.fn().mockResolvedValue(null),
      selectRacecarAsync: vi.fn(),
      loadCarConfigurationSummariesAsync: vi.fn().mockResolvedValue([
        {
          id: 'summary-1',
          lastUpdated: new Date('2026-01-02T00:00:00Z'),
          name: 'Config A',
          car: 'car-1',
          notes: 'A',
          configurationSchemaVersion: 1,
          lastUpdatedOnCarTimestamp: null
        }
      ]),
      loadReservedChannelDefinitionsAsync: vi.fn().mockResolvedValue([]),
      loadAvailableUnitTypesAsync: vi.fn().mockResolvedValue([]),
      loadCarConfigurationAsync: vi.fn().mockResolvedValue(buildConfig('Loaded Config')),
      saveCarConfigurationAsync: vi.fn(),
      transmitToCarAsync: vi.fn(),
      deleteCarConfigurationAsync: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [CarConfigurationComponent],
      providers: [{ provide: MANAGEMENT_DATA_CLIENT, useValue: mockClient }]
    }).compileComponents();
  });

  it('creates and loads summaries on init', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);

    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
    expect(mockClient.loadCarConfigurationSummariesAsync).toHaveBeenCalledTimes(1);
    expect(component.configurationSummaries().length).toBe(1);
    expect(component.loadingSummaries()).toBe(false);
  });

  it('opens a configuration and sets activeConfiguration', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    await component.openConfiguration('cfg-1');

    expect(mockClient.loadCarConfigurationAsync).toHaveBeenCalledWith('cfg-1');
    expect(component.activeConfiguration()?.name).toBe('Loaded Config');
  });

  it('adds two empty can interfaces when a loaded configuration has none', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    mockClient.loadCarConfigurationAsync = vi.fn().mockResolvedValue({
      ...buildConfig('Loaded Config'),
      canConfig: {
        canBusEnabled: [],
        interfaces: []
      }
    });

    const component = fixture.componentInstance;
    await component.openConfiguration('cfg-1');

    expect(component.activeConfiguration()?.canConfig.interfaces.map((item) => item.interfaceName)).toEqual(['can0', 'can1']);
    expect(component.activeConfiguration()?.canConfig.canBusEnabled).toEqual([false, false]);
  });

  it('shows empty-state message when no configuration summaries exist', async () => {
    const emptyClient: ManagementDataClient = {
      listDiscoveredRacecarsAsync: vi.fn().mockResolvedValue([]),
      getActiveRacecarAsync: vi.fn().mockResolvedValue(null),
      selectRacecarAsync: vi.fn(),
      loadCarConfigurationSummariesAsync: vi.fn().mockResolvedValue([]),
      loadReservedChannelDefinitionsAsync: vi.fn().mockResolvedValue([]),
      loadAvailableUnitTypesAsync: vi.fn().mockResolvedValue([]),
      loadCarConfigurationAsync: vi.fn().mockResolvedValue(buildConfig('Loaded Config')),
      saveCarConfigurationAsync: vi.fn(),
      transmitToCarAsync: vi.fn(),
      deleteCarConfigurationAsync: vi.fn()
    };

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [CarConfigurationComponent],
      providers: [{ provide: MANAGEMENT_DATA_CLIENT, useValue: emptyClient }]
    }).compileComponents();

    const fixture = TestBed.createComponent(CarConfigurationComponent);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('There are no existing configurations, select New to start one');
    expect(text).not.toContain('Loading configuration summaries...');
  });

  it('enables can-bus node when navigating from can-bus error', () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    const component = fixture.componentInstance;

    component.canBusEnabled.set(false);
    component.selectedNodeId.set('general-settings');

    component.navigateFromError('can-bus');

    expect(component.canBusEnabled()).toBe(true);
    expect(component.selectedNodeId()).toBe('can-bus');
    expect(component.treeNodes().length).toBeGreaterThan(0);
  });

  it('moves selection to communications when can-bus is disabled while selected', () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    const component = fixture.componentInstance;

    component.selectedNodeId.set('can-bus');

    const event = {
      target: { checked: false }
    } as unknown as Event;

    component.onCanBusFeatureToggle(event);

    expect(component.canBusEnabled()).toBe(false);
    expect(component.selectedNodeId()).toBe('communications');
  });

  it('updates active configuration cloud connection flag when cloud communications toggle changes', () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    const component = fixture.componentInstance;

    component.activeConfiguration.set({ ...buildConfig('Config A'), isCloudConnectionEnabled: false });

    component.onCloudConnectionEnabledChange(true);

    expect(component.activeConfiguration()?.isCloudConnectionEnabled).toBe(true);
  });

  it('shows cloud configuration tree node only when cloud connection is enabled', () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    const component = fixture.componentInstance;

    component.activeConfiguration.set({ ...buildConfig('Config A'), isCloudConnectionEnabled: false });
    const nodeBeforeEnable = component
      .treeNodes()
      .find((node) => node.id === 'communications')
      ?.children
      ?.find((child) => child.id === 'cloud-configuration');

    component.onCloudConnectionEnabledChange(true);
    const nodeAfterEnable = component
      .treeNodes()
      .find((node) => node.id === 'communications')
      ?.children
      ?.find((child) => child.id === 'cloud-configuration');

    expect(nodeBeforeEnable?.visible).toBe(false);
    expect(nodeAfterEnable?.visible).toBe(true);
  });

  it('updates cloud credentials when cloud configuration form emits changes', () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    const component = fixture.componentInstance;

    component.activeConfiguration.set({ ...buildConfig('Config A'), clientId: '', clientSecret: '' });

    component.onCloudConfigurationChange({
      clientId: 'client-123',
      clientSecret: 'secret-456'
    });

    expect(component.activeConfiguration()?.clientId).toBe('client-123');
    expect(component.activeConfiguration()?.clientSecret).toBe('secret-456');
  });

  it('requires cloud credentials when cloud communications is enabled', () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    const component = fixture.componentInstance;

    component.activeConfiguration.set({ ...buildConfig('Config A'), clientId: '', clientSecret: '' });
    component.onCloudConnectionEnabledChange(true);

    const messages = component.validationErrors().map((item) => item.message);
    expect(messages).toContain('Client ID is required and must be 64 characters or fewer.');
    expect(messages).toContain('Client Secret is required and must be 32 characters or fewer.');
  });

  it('saves active configuration and updates state', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    const saved = buildConfig('Saved Config');
    (mockClient.saveCarConfigurationAsync as ReturnType<typeof vi.fn>).mockResolvedValue(saved);

    component.activeConfiguration.set(buildConfig('Before Save'));
    await component.saveConfiguration();

    expect(mockClient.saveCarConfigurationAsync).toHaveBeenCalledTimes(1);
    expect(component.activeConfiguration()?.name).toBe('Saved Config');
    expect(component.snackbarMessage()).toBe('Configuration saved.');
  });

  it('transmits active configuration and updates state from response', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    const transmitted = buildConfig('Transmitted Config');
    (mockClient.transmitToCarAsync as ReturnType<typeof vi.fn>).mockResolvedValue(transmitted);

    component.activeConfiguration.set(buildConfig('Before Transmit'));
    await component.transmitToCar();

    expect(mockClient.transmitToCarAsync).toHaveBeenCalledTimes(1);
    expect(component.activeConfiguration()?.name).toBe('Transmitted Config');
    expect(component.snackbarMessage()).toBe('Configuration transmitted to car.');
  });

  it('does not save when configuration is invalid', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.activeConfiguration.set({ ...buildConfig('Valid Name'), name: '' });

    await component.saveConfiguration();

    expect(mockClient.saveCarConfigurationAsync).not.toHaveBeenCalled();
    expect(component.errorDialogMessage()).toBe('Please resolve validation errors before saving.');
  });

  it('does not transmit when configuration is invalid', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.activeConfiguration.set({ ...buildConfig('Valid Name'), name: '' });

    await component.transmitToCar();

    expect(mockClient.transmitToCarAsync).not.toHaveBeenCalled();
    expect(component.errorDialogMessage()).toBe('Please resolve validation errors before transmitting to car.');
  });

  it('shows dialog when save request fails', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    (mockClient.saveCarConfigurationAsync as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Server unavailable'));

    component.activeConfiguration.set(buildConfig('Valid Name'));
    await component.saveConfiguration();

    expect(component.errorDialogMessage()).toContain('Unable to save configuration.');
  });

  it('shows dialog when transmit request fails', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    (mockClient.transmitToCarAsync as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Car offline'));

    component.activeConfiguration.set(buildConfig('Valid Name'));
    await component.transmitToCar();

    expect(component.errorDialogMessage()).toContain('Unable to transmit configuration to car.');
  });

  it('deletes a configuration from the list and shows snackbar', async () => {
    // deleteConfiguration opens a MatDialog confirmation and awaits afterClosed().
    // Mock MatDialog so open(...).afterClosed() resolves to `true` (user confirms).
    const dialogMock = {
      open: vi.fn().mockReturnValue({ afterClosed: () => of(true) })
    };

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [CarConfigurationComponent],
      providers: [
        { provide: MANAGEMENT_DATA_CLIENT, useValue: mockClient },
        { provide: MatDialog, useValue: dialogMock }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    (mockClient.deleteCarConfigurationAsync as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);

    await component.deleteConfiguration('summary-1');

    expect(mockClient.deleteCarConfigurationAsync).toHaveBeenCalledWith('summary-1');
    expect(component.snackbarMessage()).toBe('Configuration deleted.');
  });

  describe('Fuel Analysis channel auto-injection', () => {
    const fuelReservedA = buildReservedChannel('fuel-1', 'FuelRangeMinutes', 'fuel-analysis');
    const fuelReservedB = buildReservedChannel('fuel-2', 'RaceFlagState', 'fuel-analysis');
    const throttleReservedA = buildReservedChannel('throttle-1', 'ThrottlePosition', 'throttle-consumption');
    const throttleReservedB = buildReservedChannel('throttle-2', 'ThrottleProxyFuelUsed', 'throttle-consumption');
    const unmanagedReserved = buildReservedChannel('unmanaged-1', 'CoolantTemp', null);

    async function buildFixtureWithReservedChannels() {
      const client: ManagementDataClient = {
        ...mockClient,
        loadReservedChannelDefinitionsAsync: vi.fn().mockResolvedValue([
          fuelReservedA,
          fuelReservedB,
          throttleReservedA,
          throttleReservedB,
          unmanagedReserved
        ])
      };

      TestBed.resetTestingModule();
      await TestBed.configureTestingModule({
        imports: [CarConfigurationComponent],
        providers: [{ provide: MANAGEMENT_DATA_CLIENT, useValue: client }]
      }).compileComponents();

      const fixture = TestBed.createComponent(CarConfigurationComponent);
      fixture.detectChanges();
      await fixture.whenStable();
      return fixture;
    }

    it('injects fuel-analysis reserved channels when fuel analysis is toggled on', async () => {
      const fixture = await buildFixtureWithReservedChannels();
      const component = fixture.componentInstance;
      component.activeConfiguration.set(buildConfig('Config A'));

      component.onFuelConfigChange({
        isEnabled: true,
        tankCapacityGallons: 15,
        defaultConsumptionGalPerMin: 0.3,
        defaultYellowConsumptionMultiplier: 0.5,
        defaultCode35ConsumptionMultiplier: 0.3,
        ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: false, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
      });

      const ids = component.activeConfiguration()!.channelDefinitions.map((c) => c.id);
      expect(ids).toContain('fuel-1');
      expect(ids).toContain('fuel-2');
      expect(ids).not.toContain('throttle-1');
    });

    it('removes fuel-analysis and throttle channels when fuel analysis is toggled off (cascade)', async () => {
      const fixture = await buildFixtureWithReservedChannels();
      const component = fixture.componentInstance;
      component.activeConfiguration.set({
        ...buildConfig('Config A'),
        channelDefinitions: [
          unmanagedReserved,
          fuelReservedA,
          fuelReservedB,
          throttleReservedA,
          throttleReservedB
        ],
        fuelConfig: {
          isEnabled: true,
          tankCapacityGallons: 15,
          defaultConsumptionGalPerMin: 0.3,
          defaultYellowConsumptionMultiplier: 0.5,
          defaultCode35ConsumptionMultiplier: 0.3,
          ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: true, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
        }
      });

      component.onFuelConfigChange({
        isEnabled: false,
        tankCapacityGallons: 15,
        defaultConsumptionGalPerMin: 0.3,
        defaultYellowConsumptionMultiplier: 0.5,
        defaultCode35ConsumptionMultiplier: 0.3,
        ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: true, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
      });

      const ids = component.activeConfiguration()!.channelDefinitions.map((c) => c.id);
      expect(ids).toEqual(['unmanaged-1']);
      expect(component.activeConfiguration()!.fuelConfig.throttleConsumption.isEnabled).toBe(false);
    });

    it('adds throttle channels when throttle is toggled on (fuel already on)', async () => {
      const fixture = await buildFixtureWithReservedChannels();
      const component = fixture.componentInstance;
      component.activeConfiguration.set({
        ...buildConfig('Config A'),
        channelDefinitions: [fuelReservedA, fuelReservedB],
        fuelConfig: {
          isEnabled: true,
          tankCapacityGallons: 15,
          defaultConsumptionGalPerMin: 0.3,
          defaultYellowConsumptionMultiplier: 0.5,
          defaultCode35ConsumptionMultiplier: 0.3,
          ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: false, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
        }
      });

      component.onFuelConfigChange({
        isEnabled: true,
        tankCapacityGallons: 15,
        defaultConsumptionGalPerMin: 0.3,
        defaultYellowConsumptionMultiplier: 0.5,
        defaultCode35ConsumptionMultiplier: 0.3,
        ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: true, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
      });

      const ids = component.activeConfiguration()!.channelDefinitions.map((c) => c.id);
      expect(ids).toEqual(['fuel-1', 'fuel-2', 'throttle-1', 'throttle-2']);
    });

    it('removes only throttle channels when throttle is toggled off and fuel stays on', async () => {
      const fixture = await buildFixtureWithReservedChannels();
      const component = fixture.componentInstance;
      component.activeConfiguration.set({
        ...buildConfig('Config A'),
        channelDefinitions: [fuelReservedA, fuelReservedB, throttleReservedA, throttleReservedB],
        fuelConfig: {
          isEnabled: true,
          tankCapacityGallons: 15,
          defaultConsumptionGalPerMin: 0.3,
          defaultYellowConsumptionMultiplier: 0.5,
          defaultCode35ConsumptionMultiplier: 0.3,
          ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: true, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
        }
      });

      component.onFuelConfigChange({
        isEnabled: true,
        tankCapacityGallons: 15,
        defaultConsumptionGalPerMin: 0.3,
        defaultYellowConsumptionMultiplier: 0.5,
        defaultCode35ConsumptionMultiplier: 0.3,
        ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: false, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
      });

      const ids = component.activeConfiguration()!.channelDefinitions.map((c) => c.id);
      expect(ids).toEqual(['fuel-1', 'fuel-2']);
    });

    it('does not duplicate channels when injecting twice', async () => {
      const fixture = await buildFixtureWithReservedChannels();
      const component = fixture.componentInstance;
      component.activeConfiguration.set({
        ...buildConfig('Config A'),
        channelDefinitions: [fuelReservedA] // already has one of the managed channels
      });

      component.onFuelConfigChange({
        isEnabled: true,
        tankCapacityGallons: 15,
        defaultConsumptionGalPerMin: 0.3,
        defaultYellowConsumptionMultiplier: 0.5,
        defaultCode35ConsumptionMultiplier: 0.3,
        ...DEFAULT_FUEL_CHANNEL_IDS, throttleConsumption: { isEnabled: false, maxRpm: 7000, ...DEFAULT_THROTTLE_CHANNEL_IDS }
      });

      const ids = component.activeConfiguration()!.channelDefinitions.map((c) => c.id);
      expect(ids).toEqual(['fuel-1', 'fuel-2']);
    });
  });
});

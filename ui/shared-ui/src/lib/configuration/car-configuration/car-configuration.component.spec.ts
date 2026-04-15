import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { CarConfigurationComponent } from './car-configuration.component';
import {
  MANAGEMENT_DATA_CLIENT,
  type ManagementDataClient
} from '../../data/management-data-client';
import type { CarConfiguration } from '../../../models/car-configuration';

try {
  getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Test environment may already be initialized by the runner.
}

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

describe('CarConfigurationComponent', () => {
  let mockClient: ManagementDataClient;

  beforeEach(async () => {
    mockClient = {
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

  it('shows empty-state message when no configuration summaries exist', async () => {
    const emptyClient: ManagementDataClient = {
      loadCarConfigurationSummariesAsync: vi.fn().mockResolvedValue([]),
      loadReservedChannelDefinitionsAsync: vi.fn().mockResolvedValue([]),
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
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    (mockClient.deleteCarConfigurationAsync as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);

    await component.deleteConfiguration('summary-1');

    expect(mockClient.deleteCarConfigurationAsync).toHaveBeenCalledWith('summary-1');
    expect(component.snackbarMessage()).toBe('Configuration deleted.');
  });
});

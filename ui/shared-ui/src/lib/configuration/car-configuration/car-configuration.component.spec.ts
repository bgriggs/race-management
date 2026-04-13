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
    car: 'car-1',
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
          notes: 'A'
        }
      ]),
      loadCarConfigurationAsync: vi.fn().mockResolvedValue(buildConfig('Loaded Config')),
      saveCarConfigurationAsync: vi.fn(),
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
      loadCarConfigurationAsync: vi.fn().mockResolvedValue(buildConfig('Loaded Config')),
      saveCarConfigurationAsync: vi.fn(),
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
});

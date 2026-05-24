import { TestBed } from '@angular/core/testing';
import { CarConfigurationComponent } from '../../../../shared-ui/src/lib/configuration/car-configuration/car-configuration.component';
import {
  MANAGEMENT_DATA_CLIENT,
  type ManagementDataClient
} from '../../../../shared-ui/src/lib/data/management-data-client';
import type { CarConfiguration } from '../../../../shared-ui/src/models/car-configuration';

function buildConfig(name: string): CarConfiguration {
  return {
    configurationId: 'cfg-1',
    configurationSchemaVersion: 1,
    name,
    notes: 'notes',
    lastUpdated: new Date('2026-01-01T00:00:00Z'),
    lastUpdatedOnCarTimestamp: null,
    car: 'car-1',
    isCloudConnectionEnabled: false,
    clientId: 'client-id',
    clientSecret: 'client-secret',
    canConfig: {
      canBusEnabled: [false, false],
      interfaces: []
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
      throttleConsumption: { isEnabled: false, maxRpm: 7000 }
    }
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
          notes: 'A'
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

  it('loads summaries on init', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);

    fixture.detectChanges();
    await fixture.whenStable();

    expect(mockClient.loadCarConfigurationSummariesAsync).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.configurationSummaries().length).toBe(1);
  });

  it('opens a configuration', async () => {
    const fixture = TestBed.createComponent(CarConfigurationComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    await component.openConfiguration('cfg-1');

    expect(mockClient.loadCarConfigurationAsync).toHaveBeenCalledWith('cfg-1');
    expect(component.activeConfiguration()?.name).toBe('Loaded Config');
  });
});

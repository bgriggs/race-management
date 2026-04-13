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

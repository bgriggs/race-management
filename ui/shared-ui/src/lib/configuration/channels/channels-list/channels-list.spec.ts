import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChannelsList } from './channels-list';
import type { CarConfiguration } from '../../../../models/car-configuration';
import { ChannelDistribution } from '../../../../models/channel-distribution';
import { ChannelScope } from '../../../../models/channel-scope';
import { By } from '@angular/platform-browser';
import { MANAGEMENT_DATA_CLIENT, type ManagementDataClient } from '../../../data/management-data-client';

function buildConfigurationWithChannels(channelCount: number): CarConfiguration {
  return {
    configurationId: 'cfg-1',
    configurationSchemaVersion: 1,
    name: 'Config',
    notes: '',
    lastUpdated: new Date('2026-01-01T00:00:00Z'),
    lastUpdatedOnCarTimestamp: null,
    car: 'car-1',
    isCloudConnectionEnabled: false,
    clientId: '',
    clientSecret: '',
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
    channelDefinitions: Array.from({ length: channelCount }, (_, index) => ({
      id: `channel-${index + 1}`,
      isReserved: index === 0,
      category: 'Engine',
      name: `Channel ${index + 1}`,
      abbreviation: `C${index + 1}`,
      dataType: 'number',
      baseUnitType: 'Celsius',
      outputUnitType: 'Celsius',
      outputDecimalPlaces: 1,
      lowRange: 0,
      highRange: 100,
      defaultValue: 0,
      groupTag: 'Powertrain',
      enumConversion: null,
      timeoutMs: 3000,
      distribution: ChannelDistribution.CarToCloud,
      scope: ChannelScope.PerCar,
      managedByFeature: null,
    })),
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
      defaultYellowConsumptionMultiplier: 1,
      defaultCode35ConsumptionMultiplier: 1,
      throttleConsumption: { isEnabled: false, maxRpm: 0 },
    },
  };
}

describe('ChannelsList', () => {
  let component: ChannelsList;
  let fixture: ComponentFixture<ChannelsList>;
  let mockClient: ManagementDataClient;

  beforeEach(async () => {
    mockClient = {
      listDiscoveredRacecarsAsync: vi.fn().mockResolvedValue([]),
      getActiveRacecarAsync: vi.fn().mockResolvedValue(null),
      selectRacecarAsync: vi.fn(),
      loadCarConfigurationSummariesAsync: vi.fn(),
      loadReservedChannelDefinitionsAsync: vi.fn().mockResolvedValue([]),
      loadAvailableUnitTypesAsync: vi.fn().mockResolvedValue([]),
      loadCarConfigurationAsync: vi.fn(),
      saveCarConfigurationAsync: vi.fn(),
      transmitToCarAsync: vi.fn(),
      deleteCarConfigurationAsync: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [ChannelsList],
      providers: [{ provide: MANAGEMENT_DATA_CLIENT, useValue: mockClient }]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ChannelsList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('shows a no channels message when configuration has no channels', () => {
    fixture.componentRef.setInput('configuration', buildConfigurationWithChannels(0));
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('There are no channels configured.');
  });

  it('renders channel rows and shows a checkmark when channel is reserved', () => {
    fixture.componentRef.setInput('configuration', buildConfigurationWithChannels(2));
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Name');
    expect(text).toContain('Reserved');
    expect(text).toContain('Channel 1');
    expect(text).toContain('Channel 2');
    expect(text).toContain('✓');
  });

  it('shows add channel button', () => {
    fixture.componentRef.setInput('configuration', buildConfigurationWithChannels(0));
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Add Channel');
  });

  it('emits updated list when deleting a channel', () => {
    fixture.componentRef.setInput('configuration', buildConfigurationWithChannels(2));
    const emitSpy = vi.spyOn(component.channelDefinitionsChange, 'emit');
    fixture.detectChanges();

    const deleteButtons = fixture.debugElement.queryAll(By.css('button[aria-label="Delete channel"]'));
    deleteButtons[0].nativeElement.click();

    expect(emitSpy).toHaveBeenCalledTimes(1);
    const updatedChannels = emitSpy.mock.calls[0][0] as Array<{ id: string }>;
    expect(updatedChannels.length).toBe(1);
    expect(updatedChannels[0].id).toBe('channel-2');
  });

  it('opens editor when edit button is clicked', () => {
    fixture.componentRef.setInput('configuration', buildConfigurationWithChannels(1));
    fixture.detectChanges();

    const editButton = fixture.debugElement.query(By.css('button[aria-label="Edit channel"]'));
    editButton.nativeElement.click();
    fixture.detectChanges();

    expect(component.isEditing()).toBe(true);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Edit Channel');
  });
});

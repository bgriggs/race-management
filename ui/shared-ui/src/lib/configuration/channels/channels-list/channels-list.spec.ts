import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChannelsList } from './channels-list';
import type { CarConfiguration } from '../../../../models/car-configuration';

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
    channelDefinitions: Array.from({ length: channelCount }, (_, index) => ({
      id: `channel-${index + 1}`,
      isReserved: index === 0,
      category: 'Engine',
      name: `Channel ${index + 1}`,
      abbreviation: `C${index + 1}`,
      dataType: 'number',
      isStringValue: false,
      baseUnitType: 'Celsius',
      baseDecimalPlaces: 1,
      outputUnitType: 'Celsius',
      outputDecimalPlaces: 1,
      lowRange: 0,
      highRange: 100,
      groupTag: 'Powertrain'
    })),
    counterDefinitions: [],
    mathDefinitions: [],
    tableMappings: [],
    timerDefinitions: [],
    userConditions: []
  };
}

describe('ChannelsList', () => {
  let component: ChannelsList;
  let fixture: ComponentFixture<ChannelsList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChannelsList]
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
    expect(text).toContain('IsReserved');
    expect(text).toContain('Channel 1');
    expect(text).toContain('Channel 2');
    expect(text).toContain('✓');
  });
});

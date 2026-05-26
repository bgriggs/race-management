import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UserConditionsList } from './user-conditions-list';
import { CarConfiguration } from '../../../../models/car-configuration';

const buildConfiguration = (): CarConfiguration => ({
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
      { interfaceName: 'can0', bitRate: 1000000, silentOnCanBus: false, messages: [] },
      { interfaceName: 'can1', bitRate: 1000000, silentOnCanBus: false, messages: [] },
    ],
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
    defaultYellowConsumptionMultiplier: 1,
    defaultCode35ConsumptionMultiplier: 1,
    tripFuelChannelId: 'acd3d127-acaf-4f8a-b27a-8623cfda09f3',
    fuelUsedChannelId: '740ce2a6-dc88-4425-85dc-7f99f2a902f1',
    fuelFullChannelId: 'c3b94831-95f6-4935-bf67-1aacfd611f75',
    inPitChannelId: 'da12563a-1167-4899-9956-700b0b693005',
    throttleConsumption: {
      isEnabled: false,
      maxRpm: 0,
      throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
      engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
    },
  },
});

describe('UserConditionsList', () => {
  let component: UserConditionsList;
  let fixture: ComponentFixture<UserConditionsList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserConditionsList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UserConditionsList);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('configuration', buildConfiguration());
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

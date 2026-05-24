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
    throttleConsumption: { isEnabled: false, maxRpm: 0 },
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

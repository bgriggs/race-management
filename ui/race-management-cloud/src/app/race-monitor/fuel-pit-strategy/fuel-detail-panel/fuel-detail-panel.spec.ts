import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { Race } from '../../../../../../shared-ui/src/cloud-api/race';
import { FuelClient } from '../../../clients/fuel-client';
import { FuelDetailPanel } from './fuel-detail-panel';

describe('FuelDetailPanel', () => {
  let component: FuelDetailPanel;
  let fixture: ComponentFixture<FuelDetailPanel>;

  const car: Car = { teamId: 1, number: '42', make: '', model: '', color: '' };
  const race: Race = {
    id: 7,
    teamId: 1,
    name: 'Test Race',
    start: new Date('2026-05-24T13:00:00Z'),
    duration: 4,
    notes: '',
    redMistEventId: null,
    redMistOrganizationId: null,
  };

  beforeEach(async () => {
    const fuelStub = {
      loadFuelSnapshot: vi.fn().mockResolvedValue(null),
      loadFuelWindows: vi.fn().mockResolvedValue([]),
      loadCalibrationFactor: vi.fn().mockResolvedValue(null),
      saveCalibrationOverride: vi.fn().mockResolvedValue({}),
      resetCalibration: vi.fn().mockResolvedValue({}),
      resumeCalibrationLearning: vi.fn().mockResolvedValue({}),
    } as unknown as FuelClient;

    await TestBed.configureTestingModule({
      imports: [FuelDetailPanel],
      providers: [{ provide: FuelClient, useValue: fuelStub }],
    }).compileComponents();

    fixture = TestBed.createComponent(FuelDetailPanel);
    fixture.componentRef.setInput('teamId', 1);
    fixture.componentRef.setInput('car', car);
    fixture.componentRef.setInput('race', race);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

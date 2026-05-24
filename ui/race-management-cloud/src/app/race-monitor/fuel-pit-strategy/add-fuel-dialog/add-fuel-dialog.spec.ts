import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Car } from '../../../../../../shared-ui/src/cloud-api/car';
import { FuelClient } from '../../../clients/fuel-client';
import { AddFuelDialog } from './add-fuel-dialog';

describe('AddFuelDialog', () => {
  let component: AddFuelDialog;
  let fixture: ComponentFixture<AddFuelDialog>;

  const car: Car = { teamId: 1, number: '42', make: '', model: '', color: '' };

  beforeEach(async () => {
    const fuelStub = {
      saveManualRefuel: vi.fn().mockResolvedValue({ atUtc: new Date(), gallons: 10 }),
      enterRefuelVolume: vi.fn().mockResolvedValue({ atUtc: new Date(), gallons: 10 }),
    } as unknown as FuelClient;

    await TestBed.configureTestingModule({
      imports: [AddFuelDialog],
      providers: [{ provide: FuelClient, useValue: fuelStub }],
    }).compileComponents();

    fixture = TestBed.createComponent(AddFuelDialog);
    fixture.componentRef.setInput('teamId', 1);
    fixture.componentRef.setInput('car', car);
    fixture.componentRef.setInput('mode', { kind: 'manual' });
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

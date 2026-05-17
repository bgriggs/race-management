import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FuelPitStrategy } from './fuel-pit-strategy';

describe('FuelPitStrategy', () => {
  let component: FuelPitStrategy;
  let fixture: ComponentFixture<FuelPitStrategy>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FuelPitStrategy]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FuelPitStrategy);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

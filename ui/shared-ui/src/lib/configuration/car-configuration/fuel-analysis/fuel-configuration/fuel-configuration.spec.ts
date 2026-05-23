import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

import { FuelConfiguration } from './fuel-configuration';

describe('FuelConfiguration', () => {
  let component: FuelConfiguration;
  let fixture: ComponentFixture<FuelConfiguration>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FuelConfiguration, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(FuelConfiguration);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

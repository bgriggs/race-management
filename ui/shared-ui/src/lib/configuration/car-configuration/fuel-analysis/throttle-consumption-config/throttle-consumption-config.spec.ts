import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThrottleConsumptionConfig } from './throttle-consumption-config';

describe('ThrottleConsumptionConfig', () => {
  let component: ThrottleConsumptionConfig;
  let fixture: ComponentFixture<ThrottleConsumptionConfig>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ThrottleConsumptionConfig],
    }).compileComponents();

    fixture = TestBed.createComponent(ThrottleConsumptionConfig);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

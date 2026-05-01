import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CanBusConfig } from './can-bus-config';

describe('CanBusConfig', () => {
  let component: CanBusConfig;
  let fixture: ComponentFixture<CanBusConfig>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CanBusConfig]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CanBusConfig);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

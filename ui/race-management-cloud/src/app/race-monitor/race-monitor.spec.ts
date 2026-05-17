import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RaceMonitor } from './race-monitor';

describe('RaceMonitor', () => {
  let component: RaceMonitor;
  let fixture: ComponentFixture<RaceMonitor>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RaceMonitor]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RaceMonitor);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

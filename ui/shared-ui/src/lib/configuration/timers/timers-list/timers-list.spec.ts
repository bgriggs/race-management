import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimersList } from './timers-list';

describe('TimersList', () => {
  let component: TimersList;
  let fixture: ComponentFixture<TimersList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimersList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TimersList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

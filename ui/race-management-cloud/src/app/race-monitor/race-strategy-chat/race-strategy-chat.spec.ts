import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RaceStrategyChat } from './race-strategy-chat';

describe('RaceStrategyChat', () => {
  let component: RaceStrategyChat;
  let fixture: ComponentFixture<RaceStrategyChat>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RaceStrategyChat]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RaceStrategyChat);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

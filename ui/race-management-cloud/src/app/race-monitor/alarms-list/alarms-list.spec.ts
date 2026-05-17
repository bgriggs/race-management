import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AlarmsList } from './alarms-list';

describe('AlarmsList', () => {
  let component: AlarmsList;
  let fixture: ComponentFixture<AlarmsList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AlarmsList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AlarmsList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AlarmList } from './alarm-list';

describe('AlarmList', () => {
  let component: AlarmList;
  let fixture: ComponentFixture<AlarmList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AlarmList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AlarmList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditAlarm } from './edit-alarm';

describe('EditAlarm', () => {
  let component: EditAlarm;
  let fixture: ComponentFixture<EditAlarm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditAlarm]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditAlarm);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.componentRef.setInput('alarm', {
      id: '',
      name: '',
      statement: { id: '', activateComparisons: [], deactivateComparisons: null },
      messsage: '',
      displayChannelSourceColorHex: '',
      timeAfterAckToDisplaySecs: 0,
      alarmStatusChannelId: null,
    });
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditLogEntry } from './edit-log-entry';
import { LoggingFrequency } from '../../../../models/logging-frequency';

describe('EditLogEntry', () => {
  let component: EditLogEntry;
  let fixture: ComponentFixture<EditLogEntry>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditLogEntry]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditLogEntry);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.componentRef.setInput('entry', {
      id: '',
      channelId: '',
      frequency: LoggingFrequency.OncePerSecond,
    });
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

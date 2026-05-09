import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditLogEntry } from './edit-log-entry';

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
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditCounter } from './edit-counter';

describe('EditCounter', () => {
  let component: EditCounter;
  let fixture: ComponentFixture<EditCounter>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditCounter]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditCounter);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

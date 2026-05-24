import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditTimer } from './edit-timer';

describe('EditTimer', () => {
  let component: EditTimer;
  let fixture: ComponentFixture<EditTimer>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditTimer]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditTimer);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

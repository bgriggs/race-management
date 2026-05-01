import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditUserCondition } from './edit-user-condition';

describe('EditUserCondition', () => {
  let component: EditUserCondition;
  let fixture: ComponentFixture<EditUserCondition>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditUserCondition]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditUserCondition);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

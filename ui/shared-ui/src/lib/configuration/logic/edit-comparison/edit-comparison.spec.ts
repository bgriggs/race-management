import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditComparison } from './edit-comparison';

describe('EditComparison', () => {
  let component: EditComparison;
  let fixture: ComponentFixture<EditComparison>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditComparison]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditComparison);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

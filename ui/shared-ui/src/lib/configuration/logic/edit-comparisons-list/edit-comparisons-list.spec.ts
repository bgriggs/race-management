import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditComparisonsList } from './edit-comparisons-list';

describe('EditComparisonsList', () => {
  let component: EditComparisonsList;
  let fixture: ComponentFixture<EditComparisonsList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditComparisonsList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditComparisonsList);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

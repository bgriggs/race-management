import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditEnum } from './edit-enum';

describe('EditEnum', () => {
  let component: EditEnum;
  let fixture: ComponentFixture<EditEnum>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditEnum]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditEnum);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditMath } from './edit-math';

describe('EditMath', () => {
  let component: EditMath;
  let fixture: ComponentFixture<EditMath>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditMath]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditMath);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

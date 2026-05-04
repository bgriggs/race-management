import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MathList } from './math-list';

describe('MathList', () => {
  let component: MathList;
  let fixture: ComponentFixture<MathList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MathList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MathList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

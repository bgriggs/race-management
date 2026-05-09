import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EnumList } from './enum-list';

describe('EnumList', () => {
  let component: EnumList;
  let fixture: ComponentFixture<EnumList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EnumList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EnumList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

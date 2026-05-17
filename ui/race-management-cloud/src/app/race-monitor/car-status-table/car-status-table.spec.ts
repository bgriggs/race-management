import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CarStatusTable } from './car-status-table';

describe('CarStatusTable', () => {
  let component: CarStatusTable;
  let fixture: ComponentFixture<CarStatusTable>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CarStatusTable]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CarStatusTable);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

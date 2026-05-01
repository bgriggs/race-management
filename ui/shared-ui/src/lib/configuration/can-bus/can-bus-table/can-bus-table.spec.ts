import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CanBusTable } from './can-bus-table';

describe('CanBusTable', () => {
  let component: CanBusTable;
  let fixture: ComponentFixture<CanBusTable>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CanBusTable]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CanBusTable);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

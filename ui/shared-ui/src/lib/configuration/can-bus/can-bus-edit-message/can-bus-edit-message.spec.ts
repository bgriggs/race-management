import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CanBusEditMessage } from './can-bus-edit-message';

describe('CanBusEditMessage', () => {
  let component: CanBusEditMessage;
  let fixture: ComponentFixture<CanBusEditMessage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CanBusEditMessage]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CanBusEditMessage);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

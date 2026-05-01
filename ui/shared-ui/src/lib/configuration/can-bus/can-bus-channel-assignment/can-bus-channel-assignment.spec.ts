import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CanBusChannelAssignment } from './can-bus-channel-assignment';

describe('CanBusChannelAssignment', () => {
  let component: CanBusChannelAssignment;
  let fixture: ComponentFixture<CanBusChannelAssignment>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CanBusChannelAssignment]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CanBusChannelAssignment);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

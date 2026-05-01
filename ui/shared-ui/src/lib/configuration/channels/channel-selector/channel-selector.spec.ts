import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChannelSelector } from './channel-selector';

describe('ChannelSelector', () => {
  let component: ChannelSelector;
  let fixture: ComponentFixture<ChannelSelector>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChannelSelector]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ChannelSelector);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

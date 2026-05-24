import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChannelSelectionList } from './channel-selection-list';

describe('ChannelSelectionList', () => {
  let component: ChannelSelectionList;
  let fixture: ComponentFixture<ChannelSelectionList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChannelSelectionList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ChannelSelectionList);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

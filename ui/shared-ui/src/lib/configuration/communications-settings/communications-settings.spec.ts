import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommunicationsSettings } from './communications-settings';

describe('CommunicationsSettings', () => {
  let component: CommunicationsSettings;
  let fixture: ComponentFixture<CommunicationsSettings>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommunicationsSettings]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CommunicationsSettings);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

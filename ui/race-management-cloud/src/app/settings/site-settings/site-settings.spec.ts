import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SiteSettings } from './site-settings';

describe('SiteSettings', () => {
  let component: SiteSettings;
  let fixture: ComponentFixture<SiteSettings>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SiteSettings]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SiteSettings);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

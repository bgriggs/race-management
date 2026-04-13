import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CloudConfiguration } from './cloud-configuration';

describe('CloudConfiguration', () => {
  let component: CloudConfiguration;
  let fixture: ComponentFixture<CloudConfiguration>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CloudConfiguration]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CloudConfiguration);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

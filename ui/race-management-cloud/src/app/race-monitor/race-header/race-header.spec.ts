import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { APP_CONFIG } from '../../config/app-config';
import { RaceHeader } from './race-header';

describe('RaceHeader', () => {
  let component: RaceHeader;
  let fixture: ComponentFixture<RaceHeader>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RaceHeader],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: { api: { baseUrl: 'http://localhost' } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RaceHeader);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

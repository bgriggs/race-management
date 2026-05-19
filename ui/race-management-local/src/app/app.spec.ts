import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { KEYCLOAK_EVENT_SIGNAL, KeycloakEventType } from 'keycloak-angular';
import Keycloak from 'keycloak-js';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    const keycloakStub = {
      authenticated: false,
      tokenParsed: undefined,
      login: () => Promise.resolve(),
      logout: () => Promise.resolve()
    } as unknown as Keycloak;

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: Keycloak, useValue: keycloakStub },
        {
          provide: KEYCLOAK_EVENT_SIGNAL,
          useValue: signal({ type: KeycloakEventType.KeycloakAngularNotInitialized })
        }
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render title', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('RedMist-Race Management');
    expect(compiled.querySelector('.username')?.textContent).toContain('Guest');
  });
});

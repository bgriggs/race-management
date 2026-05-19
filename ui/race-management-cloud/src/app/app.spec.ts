import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { KEYCLOAK_EVENT_SIGNAL, KeycloakEventType } from 'keycloak-angular';
import Keycloak from 'keycloak-js';
import { App } from './app';
import { AuthService } from './auth.service';
import { ConfigurationClient } from './clients/configuration-client';
import { TeamSelectionService } from './teams/team-selection.service';

describe('App', () => {
  beforeEach(async () => {
    const keycloakStub = {
      authenticated: false,
      tokenParsed: undefined,
      login: () => Promise.resolve(),
      logout: () => Promise.resolve(),
    } as unknown as Keycloak;

    const configClientStub = {
      listMyTeams: vi.fn().mockResolvedValue([]),
    } as unknown as ConfigurationClient;

    const authStub = {
      isLoggedIn: signal(false),
      displayName: signal('Guest'),
      user: signal(null),
      login: vi.fn(),
      logout: vi.fn(),
      accountManagement: vi.fn(),
    } as unknown as AuthService;

    const teamSelectionStub = {
      selectedTeamId: signal<number | null>(null),
      selectedTeam: signal(null),
      teams: signal([]),
      isAdmin: signal(false),
      loading: signal(false),
      loadFailed: signal(false),
      needsSelection: signal(false),
      hasNoTeams: signal(false),
      showsOverlay: signal(false),
      selectTeam: vi.fn(),
      retry: vi.fn(),
    } as unknown as TeamSelectionService;

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: Keycloak, useValue: keycloakStub },
        {
          provide: KEYCLOAK_EVENT_SIGNAL,
          useValue: signal({ type: KeycloakEventType.KeycloakAngularNotInitialized }),
        },
        { provide: AuthService, useValue: authStub },
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render brand and default guest user', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('Race Management');
    expect(compiled.querySelector('.username')?.textContent).toContain('Guest');
  });
});

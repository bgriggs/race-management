import { computed, effect, inject, Injectable, signal } from '@angular/core';
import {
  KEYCLOAK_EVENT_SIGNAL,
  KeycloakEventType,
  ReadyArgs,
  typeEventArgs,
} from 'keycloak-angular';
import Keycloak from 'keycloak-js';

export interface User {
  displayName: string;
  username?: string;
  email?: string;
  teamId?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly keycloak = inject(Keycloak);
  private readonly events = inject(KEYCLOAK_EVENT_SIGNAL);

  private readonly _authenticated = signal(false);
  private readonly _user = signal<User | null>(null);

  readonly isLoggedIn = computed(() => this._authenticated());
  readonly displayName = computed(() => this._user()?.displayName ?? 'Guest');
  readonly user = computed(() => this._user());

  constructor() {
    effect(() => {
      const evt = this.events();
      switch (evt.type) {
        case KeycloakEventType.Ready:
          this.applyAuthState(typeEventArgs<ReadyArgs>(evt.args));
          break;
        case KeycloakEventType.AuthSuccess:
        case KeycloakEventType.AuthRefreshSuccess:
          this.applyAuthState(true);
          break;
        case KeycloakEventType.AuthLogout:
        case KeycloakEventType.AuthError:
        case KeycloakEventType.AuthRefreshError:
        case KeycloakEventType.TokenExpired:
          this.applyAuthState(this.keycloak.authenticated ?? false);
          break;
      }
    });
  }

  login(): Promise<void> {
    return this.keycloak.login({
      redirectUri: window.location.href,
    });
  }

  logout(): Promise<void> {
    return this.keycloak.logout({
      redirectUri: window.location.origin,
    });
  }

  accountManagement(): Promise<void> {
    return this.keycloak.accountManagement();
  }

  private applyAuthState(authenticated: boolean): void {
    this._authenticated.set(authenticated);
    this._user.set(authenticated ? this.buildUser() : null);
  }

  private buildUser(): User {
    const t = this.keycloak.tokenParsed as Record<string, unknown> | undefined;
    const name = (t?.['name'] as string | undefined)
      ?? (t?.['preferred_username'] as string | undefined)
      ?? 'User';
    return {
      displayName: name,
      username: t?.['preferred_username'] as string | undefined,
      email: t?.['email'] as string | undefined,
      teamId: (t?.['team_id'] as string | undefined) ?? (t?.['teamId'] as string | undefined),
    };
  }
}

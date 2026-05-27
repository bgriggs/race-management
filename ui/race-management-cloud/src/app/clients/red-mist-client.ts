import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { OrganizationSummary } from '../../../../shared-ui/src/cloud-api/organization-summary';

const API_PREFIX = 'v1';
const CONTROLLER = 'red-mist';

@Injectable({ providedIn: 'root' })
export class RedMistClient {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  loadOrganizations(teamId: number): Promise<OrganizationSummary[]> {
    return firstValueFrom(
      this.http.get<OrganizationSummary[]>(this.url('load-organizations'), {
        params: { teamId },
      }),
    );
  }

  private url(action: string): string {
    const base = this.config.api.baseUrl.replace(/\/$/, '');
    return `${base}/${API_PREFIX}/${CONTROLLER}/${action}`;
  }
}

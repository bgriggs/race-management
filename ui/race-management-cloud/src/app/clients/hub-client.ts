import { inject, Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import Keycloak from 'keycloak-js';
import { APP_CONFIG } from '../config/app-config';
import { CarChannelSnapshot } from '../../../../shared-ui/src/cloud-api/car-channel-snapshot';
import { ChannelChangeNotification } from '../../../../shared-ui/src/cloud-api/channel-change-notification';
import {
  carChannelSnapshotFromMessagePack,
  channelChangeNotificationFromMessagePack,
} from '../../../../shared-ui/src/cloud-api/message-pack-converter';

export type HubConnectionStatus = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';

export interface ChannelValueChange {
  carKey: string;
  change: ChannelChangeNotification;
}

const HUB_PATH = '/web-status';

@Injectable({ providedIn: 'root' })
export class HubClient implements OnDestroy {
  private readonly keycloak = inject(Keycloak);
  private readonly config = inject(APP_CONFIG);

  private connection: signalR.HubConnection | null = null;
  private reconnectTimeout: ReturnType<typeof setTimeout> | null = null;

  readonly connectionStatus$ = new Subject<HubConnectionStatus>();
  readonly channelSnapshot$ = new Subject<CarChannelSnapshot[]>();
  readonly channelValueChanged$ = new Subject<ChannelValueChange>();

  async connect(): Promise<void> {
    if (this.connection) return;
    this.connection = this.buildConnection();
    this.registerLifecycleHandlers();
    await this.startWithRetry();
  }

  async disconnect(): Promise<void> {
    await this.disposeConnection();
  }

  async subscribeToTeam(teamId: number): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('SubscribeToTeam', teamId);
  }

  ngOnDestroy(): void {
    this.disposeConnection();
  }

  private buildConnection(): signalR.HubConnection {
    return new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl(), {
        accessTokenFactory: () => this.getToken(),
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withHubProtocol(new MessagePackHubProtocol())
      .withAutomaticReconnect(new InfiniteRetryPolicy())
      .configureLogging(signalR.LogLevel.Information)
      .build();
  }

  private registerLifecycleHandlers(): void {
    if (!this.connection) return;
    this.connection.onreconnecting(() => this.connectionStatus$.next('Reconnecting'));
    this.connection.onreconnected(() => this.connectionStatus$.next('Connected'));
    this.connection.onclose(() => this.connectionStatus$.next('Disconnected'));
    this.connection.on('ChannelSnapshot', (cars: unknown[]) =>
      this.channelSnapshot$.next(cars.map(c => carChannelSnapshotFromMessagePack(c as unknown[]))));
    this.connection.on('ChannelValueChanged', (carKey: string, change: unknown[]) =>
      this.channelValueChanged$.next({ carKey, change: channelChangeNotificationFromMessagePack(change) }));
  }

  private async startWithRetry(): Promise<void> {
    const maxAttempts = 100;
    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      if (!this.connection) return;
      try {
        this.connectionStatus$.next('Connecting');
        await this.connection.start();
        this.connectionStatus$.next('Connected');
        return;
      } catch (err) {
        console.warn(`Hub connection attempt ${attempt + 1} failed:`, err);
        this.connectionStatus$.next('Disconnected');
        await this.delay(5000);
      }
    }
  }

  private async getToken(): Promise<string> {
    if (this.keycloak.authenticated) {
      try {
        await this.keycloak.updateToken(30);
        return this.keycloak.token ?? '';
      } catch {
        // Token refresh failed — return empty
      }
    }
    return '';
  }

  private hubUrl(): string {
    return `${this.config.api.baseUrl.replace(/\/$/, '')}${HUB_PATH}`;
  }

  private async disposeConnection(): Promise<void> {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = null;
    }
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (err) {
        console.warn('Error stopping hub connection:', err);
      }
      this.connection = null;
      this.connectionStatus$.next('Disconnected');
    }
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => {
      this.reconnectTimeout = setTimeout(resolve, ms);
    });
  }
}

class InfiniteRetryPolicy implements signalR.IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: signalR.RetryContext): number | null {
    const delays = [0, 2000, 5000, 10000];
    return retryContext.previousRetryCount < delays.length
      ? delays[retryContext.previousRetryCount]
      : 30000;
  }
}

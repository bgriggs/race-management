import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { AuthService } from '../../auth.service';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { TelemetryStore } from '../../telemetry/telemetry-store';
import { CarStatusTable } from './car-status-table';

describe('CarStatusTable', () => {
  let component: CarStatusTable;
  let fixture: ComponentFixture<CarStatusTable>;

  beforeEach(async () => {
    const configClientStub = {
      listCars: vi.fn().mockResolvedValue([]),
      loadChannelStatusTableConfiguration: vi.fn().mockResolvedValue(null),
      loadCarConfigurationByCar: vi.fn().mockResolvedValue({ channelDefinitions: [] }),
      loadCarConfigurationById: vi.fn().mockResolvedValue({ channelDefinitions: [] }),
      saveChannelStatusTableConfiguration: vi.fn(),
    } as unknown as ConfigurationClient;

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
    } as unknown as TeamSelectionService;

    const authStub = {
      isLoggedIn: signal(false),
      displayName: signal('Guest'),
      user: signal(null),
    } as unknown as AuthService;

    const telemetryStub = {
      carsByKey: signal(new Map()),
      connectionStatus: signal('Disconnected'),
      connectedCarKeys: signal(new Set()),
      carForNumber: vi.fn().mockReturnValue(undefined),
      lastTelemetryFor: vi.fn().mockReturnValue(null),
      isCarConnected: vi.fn().mockReturnValue(false),
      disconnectedAtFor: vi.fn().mockReturnValue(null),
    } as unknown as TelemetryStore;

    await TestBed.configureTestingModule({
      imports: [CarStatusTable],
      providers: [
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
        { provide: AuthService, useValue: authStub },
        { provide: TelemetryStore, useValue: telemetryStub },
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(CarStatusTable);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  const GREEN = 'rgb(34, 197, 94)';
  const YELLOW = 'rgb(234, 179, 8)';
  const RED = 'rgb(220, 38, 38)';
  const car = { number: '42' } as never;
  const color = () => (component as never as { statusColor: (c: never) => string }).statusColor(car);
  const telemetry = () => component['telemetry'] as unknown as {
    isCarConnected: ReturnType<typeof vi.fn>;
    disconnectedAtFor: ReturnType<typeof vi.fn>;
  };

  it('is green while connected to CarHub, regardless of telemetry', () => {
    telemetry().isCarConnected.mockReturnValue(true);
    expect(color()).toBe(GREEN);
  });

  it('is yellow within the grace window after a disconnect', () => {
    telemetry().isCarConnected.mockReturnValue(false);
    const now = Date.now();
    telemetry().disconnectedAtFor.mockReturnValue(now - 1000); // 1s ago, grace is 3s
    (component as never as { now: { set: (n: number) => void } }).now.set(now);
    expect(color()).toBe(YELLOW);
  });

  it('is red once the disconnect grace window elapses', () => {
    telemetry().isCarConnected.mockReturnValue(false);
    const now = Date.now();
    telemetry().disconnectedAtFor.mockReturnValue(now - 4000); // 4s ago, past 3s grace
    (component as never as { now: { set: (n: number) => void } }).now.set(now);
    expect(color()).toBe(RED);
  });

  it('is red when never connected (no disconnect timestamp)', () => {
    telemetry().isCarConnected.mockReturnValue(false);
    telemetry().disconnectedAtFor.mockReturnValue(null);
    expect(color()).toBe(RED);
  });
});

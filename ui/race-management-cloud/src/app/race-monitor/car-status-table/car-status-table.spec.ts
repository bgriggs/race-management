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
});

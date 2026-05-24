import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { ConfigurationClient } from '../../clients/configuration-client';
import { FuelClient } from '../../clients/fuel-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { RaceSelectionService } from '../race-selection.service';
import { FuelPitStrategy } from './fuel-pit-strategy';

describe('FuelPitStrategy', () => {
  let component: FuelPitStrategy;
  let fixture: ComponentFixture<FuelPitStrategy>;

  beforeEach(async () => {
    const configClientStub = {
      listCars: vi.fn().mockResolvedValue([]),
      listRaces: vi.fn().mockResolvedValue([]),
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

    const raceSelectionStub = {
      now: signal(new Date()),
      races: signal([]),
      selectedRaceId: signal<number | null>(null),
      selectedRace: signal(null),
      activeRace: signal(null),
      selectRace: vi.fn(),
      refresh: vi.fn().mockResolvedValue(undefined),
    } as unknown as RaceSelectionService;

    const fuelStub = {
      loadFuelSnapshot: vi.fn().mockResolvedValue(null),
      loadRefuelEvents: vi.fn().mockResolvedValue([]),
      loadFuelWindows: vi.fn().mockResolvedValue([]),
      loadStints: vi.fn().mockResolvedValue([]),
    } as unknown as FuelClient;

    await TestBed.configureTestingModule({
      imports: [FuelPitStrategy],
      providers: [
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: FuelClient, useValue: fuelStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
        { provide: RaceSelectionService, useValue: raceSelectionStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FuelPitStrategy);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

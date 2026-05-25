import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';

import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { RaceSelectionService } from '../race-selection.service';
import { RaceHeader } from './race-header';

describe('RaceHeader', () => {
  let component: RaceHeader;
  let fixture: ComponentFixture<RaceHeader>;

  beforeEach(async () => {
    const configClientStub = {
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

    await TestBed.configureTestingModule({
      imports: [RaceHeader],
      providers: [
        provideRouter([]),
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
        { provide: RaceSelectionService, useValue: raceSelectionStub },
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

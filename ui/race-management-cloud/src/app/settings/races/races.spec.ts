import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { Races } from './races';

describe('Races', () => {
  let component: Races;
  let fixture: ComponentFixture<Races>;

  beforeEach(async () => {
    const configClientStub = {
      listRaces: vi.fn().mockResolvedValue([]),
      saveRace: vi.fn(),
      deleteRace: vi.fn(),
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

    await TestBed.configureTestingModule({
      imports: [Races],
      providers: [
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(Races);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

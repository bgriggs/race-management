import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { Cars } from './cars';

describe('Cars', () => {
  let component: Cars;
  let fixture: ComponentFixture<Cars>;

  beforeEach(async () => {
    const configClientStub = {
      listCars: vi.fn().mockResolvedValue([]),
      createCar: vi.fn(),
      updateCar: vi.fn(),
      deleteCar: vi.fn(),
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
      imports: [Cars],
      providers: [
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(Cars);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfigurationClient } from '../../clients/configuration-client';
import { Teams } from './teams';

describe('Teams', () => {
  let component: Teams;
  let fixture: ComponentFixture<Teams>;

  beforeEach(async () => {
    const configClientStub = {
      loadTeams: vi.fn().mockResolvedValue([]),
      createTeam: vi.fn(),
      updateTeam: vi.fn(),
      deleteTeam: vi.fn(),
    } as unknown as ConfigurationClient;

    await TestBed.configureTestingModule({
      imports: [Teams],
      providers: [{ provide: ConfigurationClient, useValue: configClientStub }],
    })
    .compileComponents();

    fixture = TestBed.createComponent(Teams);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

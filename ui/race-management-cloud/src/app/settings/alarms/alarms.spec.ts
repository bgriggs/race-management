import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { AlarmDefinitionDto } from '../../../../../shared-ui/src/cloud-api/alarm-definition-dto';
import { LogicType } from '../../../../../shared-ui/src/cloud-api/logic-type';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';
import { Alarms } from './alarms';

function makeAlarm(overrides: Partial<AlarmDefinitionDto> = {}): AlarmDefinitionDto {
  return {
    id: overrides.id ?? '00000000-0000-0000-0000-000000000001',
    teamId: 1,
    carNumber: null,
    name: overrides.name ?? 'Alarm',
    message: '',
    displayChannelSourceColorHex: '',
    timeAfterAckToDisplaySecs: 60,
    alarmStatusChannelId: null,
    statement: {
      id: '00000000-0000-0000-0001-000000000001',
      activateComparisons: [[]],
      deactivateComparisons: null,
    },
    ...overrides,
  };
}

describe('Alarms', () => {
  let component: Alarms;
  let fixture: ComponentFixture<Alarms>;

  beforeEach(async () => {
    const configClientStub = {
      loadAlarmDefinitions: vi.fn().mockResolvedValue([]),
      listCars: vi.fn().mockResolvedValue([]),
      loadCarConfigurationByCar: vi.fn(),
      saveAlarmDefinition: vi.fn(),
      deleteAlarmDefinition: vi.fn(),
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
      imports: [Alarms],
      providers: [
        { provide: ConfigurationClient, useValue: configClientStub },
        { provide: TeamSelectionService, useValue: teamSelectionStub },
      ],
    })
    .compileComponents();

    fixture = TestBed.createComponent(Alarms);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('visibleAlarms filters by scope', () => {
    const teamAlarm = makeAlarm({ id: 'a1', carNumber: null });
    const car42 = makeAlarm({ id: 'a2', carNumber: '42' });
    const car88 = makeAlarm({ id: 'a3', carNumber: '88' });
    (component as unknown as { alarms: ReturnType<typeof signal<AlarmDefinitionDto[]>> }).alarms
      .set([teamAlarm, car42, car88]);

    (component as unknown as { onFilterChanged(v: string): void }).onFilterChanged('all');
    expect((component as unknown as { visibleAlarms(): AlarmDefinitionDto[] }).visibleAlarms()).toHaveLength(3);

    (component as unknown as { onFilterChanged(v: string): void }).onFilterChanged('team');
    expect((component as unknown as { visibleAlarms(): AlarmDefinitionDto[] }).visibleAlarms())
      .toEqual([teamAlarm]);

    (component as unknown as { onFilterChanged(v: string): void }).onFilterChanged('car:42');
    expect((component as unknown as { visibleAlarms(): AlarmDefinitionDto[] }).visibleAlarms())
      .toEqual([car42]);
  });

  it('scopeLabel reflects team vs car scope', () => {
    const team = makeAlarm({ carNumber: null });
    const car = makeAlarm({ carNumber: '42' });
    const c = component as unknown as { scopeLabel(a: AlarmDefinitionDto): string };
    expect(c.scopeLabel(team)).toBe('All cars');
    expect(c.scopeLabel(car)).toBe('Car #42');
  });

  it('statementSummary renders a one-line condition with operator and rhs', () => {
    const alarm = makeAlarm({
      statement: {
        id: 's1',
        activateComparisons: [[{
          id: 'c1',
          channelId: 'ch-coolant',
          logic: LogicType.GreaterThan,
          useStaticComparison: true,
          staticValueComparison: '230',
          channelComparisonId: null,
          forMs: 0,
          reverseResult: false,
        }]],
        deactivateComparisons: null,
      },
    });

    const summary = (component as unknown as { statementSummary(a: AlarmDefinitionDto): string }).statementSummary(alarm);
    // Channel name lookup falls back to "(channel)" when nothing is cached.
    expect(summary).toBe('(channel) > 230');
  });

  it('statementSummary reports empty when no comparisons exist', () => {
    const alarm = makeAlarm();
    expect((component as unknown as { statementSummary(a: AlarmDefinitionDto): string }).statementSummary(alarm))
      .toBe('(no conditions)');
  });

  it('hasColor detects a configured hex color', () => {
    const c = component as unknown as { hasColor(a: AlarmDefinitionDto): boolean };
    expect(c.hasColor(makeAlarm({ displayChannelSourceColorHex: '' }))).toBe(false);
    expect(c.hasColor(makeAlarm({ displayChannelSourceColorHex: '#ff0000' }))).toBe(true);
    expect(c.hasColor(makeAlarm({ displayChannelSourceColorHex: '#ff0000ff' }))).toBe(true);
  });
});

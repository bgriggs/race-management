import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AlarmDefinitionDto } from '../../../../../shared-ui/src/cloud-api/alarm-definition-dto';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { EditCloudAlarm } from './edit-cloud-alarm';

function makeAlarm(overrides: Partial<AlarmDefinitionDto> = {}): AlarmDefinitionDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    teamId: 1,
    carNumber: null,
    name: 'Alarm',
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

function makeCar(number: string): Car {
  return { teamId: 1, number, make: 'Make', model: 'Model', color: 'Red' };
}

describe('EditCloudAlarm', () => {
  let fixture: ComponentFixture<EditCloudAlarm>;
  let component: EditCloudAlarm;

  async function setup(alarm: AlarmDefinitionDto, cars: Car[] = []): Promise<void> {
    await TestBed.configureTestingModule({ imports: [EditCloudAlarm] }).compileComponents();
    fixture = TestBed.createComponent(EditCloudAlarm);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('alarm', alarm);
    fixture.componentRef.setInput('channels', []);
    fixture.componentRef.setInput('cars', cars);
    fixture.componentRef.setInput('saving', false);
    fixture.componentRef.setInput('saveError', null);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('should create', async () => {
    await setup(makeAlarm());
    expect(component).toBeTruthy();
  });

  it('initializes scope=team when the input alarm has no carNumber', async () => {
    await setup(makeAlarm({ carNumber: null }), [makeCar('42')]);
    const c = component as unknown as { scope(): string; carNumber(): string | null };
    expect(c.scope()).toBe('team');
    expect(c.carNumber()).toBeNull();
  });

  it('initializes scope=car and carNumber from the input alarm', async () => {
    await setup(makeAlarm({ carNumber: '42' }), [makeCar('42')]);
    const c = component as unknown as { scope(): string; carNumber(): string | null };
    expect(c.scope()).toBe('car');
    expect(c.carNumber()).toBe('42');
  });

  it('switching to car-scope defaults to the first car', async () => {
    await setup(makeAlarm({ carNumber: null }), [makeCar('42'), makeCar('88')]);
    const c = component as unknown as { onScopeChanged(s: 'team' | 'car'): void; carNumber(): string | null };
    c.onScopeChanged('car');
    expect(c.carNumber()).toBe('42');
  });

  it('switching back to team-scope clears the carNumber', async () => {
    await setup(makeAlarm({ carNumber: '42' }), [makeCar('42')]);
    const c = component as unknown as { onScopeChanged(s: 'team' | 'car'): void; carNumber(): string | null };
    c.onScopeChanged('team');
    expect(c.carNumber()).toBeNull();
  });

  it('emits alarmChange with the new carNumber when scope changes', async () => {
    await setup(makeAlarm({ carNumber: null }), [makeCar('42')]);
    const emitted: AlarmDefinitionDto[] = [];
    component.alarmChange.subscribe((dto) => emitted.push(dto));

    const c = component as unknown as { onScopeChanged(s: 'team' | 'car'): void };
    c.onScopeChanged('car');

    expect(emitted).toHaveLength(1);
    expect(emitted[0].carNumber).toBe('42');
  });

  it('canSave is false when the name is empty', async () => {
    await setup(makeAlarm({ name: '' }));
    expect((component as unknown as { canSave(): boolean }).canSave()).toBe(false);
  });

  it('canSave is true when name is valid and scope is satisfied', async () => {
    await setup(makeAlarm({ name: 'Coolant High', carNumber: null }));
    expect((component as unknown as { canSave(): boolean }).canSave()).toBe(true);
  });

  it('canSave is false while saving even when otherwise valid', async () => {
    await setup(makeAlarm({ name: 'Coolant High', carNumber: null }));
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();
    expect((component as unknown as { canSave(): boolean }).canSave()).toBe(false);
  });
});

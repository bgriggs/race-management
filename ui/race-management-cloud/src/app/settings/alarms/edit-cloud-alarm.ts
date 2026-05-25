import { Component, computed, effect, input, output, signal } from '@angular/core';
import { AlarmDefinitionDto } from '../../../../../shared-ui/src/cloud-api/alarm-definition-dto';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { EditAlarm } from '../../../../../shared-ui/src/lib/configuration/alarms/edit-alarm/edit-alarm';
import { AlarmDefinition } from '../../../../../shared-ui/src/models/alarm-definition';
import { ChannelDefinition } from '../../../../../shared-ui/src/models/channel-definition';
import { StatementDefinition as LocalStatementDefinition } from '../../../../../shared-ui/src/models/statement-definition';

/**
 * Cloud-side wrapper around <lib-edit-alarm>. Adds the scope picker (team-level vs
 * specific car) and bridges between the cloud <code>AlarmDefinitionDto</code> shape
 * and the local <code>AlarmDefinition</code> shape <lib-edit-alarm> expects (the only
 * field-level difference is <code>message</code> on the wire vs <code>messsage</code>
 * on the shared model — a typo carried from the C# property name).
 */
type Scope = 'team' | 'car';

@Component({
  selector: 'app-edit-cloud-alarm',
  standalone: true,
  imports: [EditAlarm],
  templateUrl: './edit-cloud-alarm.html',
  styleUrl: './edit-cloud-alarm.css',
})
export class EditCloudAlarm {
  readonly alarm = input.required<AlarmDefinitionDto>();
  readonly channels = input.required<ChannelDefinition[]>();
  readonly cars = input.required<Car[]>();
  readonly saving = input<boolean>(false);
  readonly saveError = input<string | null>(null);
  readonly title = input<string>('Edit Alarm');

  readonly alarmChange = output<AlarmDefinitionDto>();
  readonly cancel = output<void>();
  readonly save = output<void>();

  protected readonly scope = signal<Scope>('team');
  protected readonly carNumber = signal<string | null>(null);

  // Mirrors <lib-edit-alarm>'s expected shape; kept in sync with the input via effect.
  protected readonly localAlarm = signal<AlarmDefinition>(emptyLocalAlarm());

  protected readonly isNameValid = computed(() => {
    const name = this.localAlarm().name.trim();
    return name.length >= 1 && name.length <= 20;
  });

  protected readonly isScopeValid = computed(() =>
    this.scope() === 'team' || (this.carNumber() !== null && this.carNumber()!.length > 0),
  );

  protected readonly canSave = computed(() => this.isNameValid() && this.isScopeValid() && !this.saving());

  constructor() {
    // Sync the cloud DTO input into the local edit shape + scope/carNumber signals.
    effect(() => {
      const dto = this.alarm();
      this.localAlarm.set(toLocal(dto));
      if (dto.carNumber) {
        this.scope.set('car');
        this.carNumber.set(dto.carNumber);
      } else {
        this.scope.set('team');
        this.carNumber.set(null);
      }
    });
  }

  protected onScopeChanged(scope: Scope): void {
    this.scope.set(scope);
    if (scope === 'team') {
      this.carNumber.set(null);
    } else if (this.carNumber() === null && this.cars().length > 0) {
      this.carNumber.set(this.cars()[0].number);
    }
    this.emitChange();
  }

  protected onCarNumberChanged(carNumber: string): void {
    this.carNumber.set(carNumber);
    this.emitChange();
  }

  protected onLocalAlarmChanged(updated: AlarmDefinition): void {
    this.localAlarm.set(updated);
    this.emitChange();
  }

  protected onCancel(): void {
    this.cancel.emit();
  }

  protected onSave(): void {
    if (!this.canSave()) return;
    this.save.emit();
  }

  private emitChange(): void {
    this.alarmChange.emit(toDto(this.localAlarm(), this.alarm(), this.scope() === 'team' ? null : this.carNumber()));
  }
}

function emptyLocalAlarm(): AlarmDefinition {
  return {
    id: '',
    name: '',
    statement: { id: '', activateComparisons: [[]], deactivateComparisons: null },
    messsage: '',
    displayChannelSourceColorHex: '',
    alarmStatusChannelId: null,
    timeAfterAckToDisplaySecs: 0,
  };
}

function toLocal(dto: AlarmDefinitionDto): AlarmDefinition {
  return {
    id: dto.id,
    name: dto.name,
    // Structural cast: cloud-api and local StatementDefinition share the same shape;
    // only the nominal enum identity differs across the two generated copies.
    statement: dto.statement as unknown as LocalStatementDefinition,
    messsage: dto.message,
    displayChannelSourceColorHex: dto.displayChannelSourceColorHex,
    alarmStatusChannelId: dto.alarmStatusChannelId,
    timeAfterAckToDisplaySecs: dto.timeAfterAckToDisplaySecs,
  };
}

function toDto(local: AlarmDefinition, base: AlarmDefinitionDto, carNumber: string | null): AlarmDefinitionDto {
  return {
    id: local.id || base.id,
    teamId: base.teamId,
    carNumber,
    name: local.name,
    message: local.messsage,
    displayChannelSourceColorHex: local.displayChannelSourceColorHex,
    timeAfterAckToDisplaySecs: local.timeAfterAckToDisplaySecs,
    alarmStatusChannelId: local.alarmStatusChannelId,
    statement: local.statement as unknown as AlarmDefinitionDto['statement'],
  };
}

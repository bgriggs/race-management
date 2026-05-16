import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MANAGEMENT_DATA_CLIENT } from '../../../data/management-data-client';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { createGuid } from '../../../utils/guid';
import { EnumDefinition } from '../../../../models/enum-definition';

type ChannelKind = 'reserved' | 'custom';
type ChannelDataType =
  'Unitless'
  | 'String'
  | 'Temperature'
  | 'Length'
  | 'Volume'
  | 'VolumeFlow'
  | 'Duration'
  | 'Speed'
  | 'Pressure'
  | 'Force'
  | 'Voltage'
  | 'Mass'
  | 'Ratio'
  | 'Current'
  | 'Resistance';

@Component({
  selector: 'lib-edit-channel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './edit-channel.html',
  styleUrl: './edit-channel.css',
})
export class EditChannel implements OnInit {
  private readonly managementDataClient = inject(MANAGEMENT_DATA_CLIENT);

  readonly channel = input<ChannelDefinition | null>(null);
  readonly existingChannels = input<ChannelDefinition[]>([]);
  readonly enumDefinitions = input<EnumDefinition[]>([]);

  readonly save = output<ChannelDefinition>();
  readonly cancel = output<void>();

  readonly kind = signal<ChannelKind>('custom');
  readonly reservedChannels = signal<ChannelDefinition[]>([]);
  readonly loadingReserved = signal(false);
  readonly reservedLoadError = signal<string | null>(null);
  readonly availableUnitTypes = signal<string[]>([]);
  readonly loadingUnitTypes = signal(false);
  readonly unitTypesLoadError = signal<string | null>(null);

  readonly dataTypeOptions: ChannelDataType[] = [
    'Unitless',
    'String',
    'Temperature',
    'Length',
    'Volume',
    'VolumeFlow',
    'Duration',
    'Speed',
    'Pressure',
    'Force',
    'Voltage',
    'Mass',
    'Ratio',
    'Current',
    'Resistance'
  ];

  readonly form = new FormGroup({
    reservedChannelId: new FormControl('', { nonNullable: true }),
    name: new FormControl('', {
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(16)],
      nonNullable: true
    }),
    abbreviation: new FormControl('', {
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(4)],
      nonNullable: true
    }),
    dataType: new FormControl<ChannelDataType>('Temperature', { nonNullable: true }),
    baseUnitType: new FormControl('', { nonNullable: true }),
    baseDecimalPlaces: new FormControl(1, { nonNullable: true }),
    outputUnitType: new FormControl('', { nonNullable: true }),
    outputDecimalPlaces: new FormControl(1, { nonNullable: true }),
    category: new FormControl('', {
      validators: [Validators.maxLength(16)],
      nonNullable: true
    }),
    groupTag: new FormControl('', {
      validators: [Validators.maxLength(16)],
      nonNullable: true
    }),
    enumConversion: new FormControl('', { nonNullable: true }),
    lowRange: new FormControl(0, { nonNullable: true }),
    highRange: new FormControl(100, { nonNullable: true }),
    defaultValue: new FormControl(0, { nonNullable: true }),
    timeoutMs: new FormControl(0, { nonNullable: true })
  });

  private readonly dataTypeValue = toSignal(this.form.controls.dataType.valueChanges, { initialValue: this.form.controls.dataType.value });
  readonly isUnitless = computed(() => this.dataTypeValue() === 'Unitless');
  readonly isString = computed(() => this.dataTypeValue() === 'String');

  readonly availableReservedChannels = computed(() => {
    const currentChannelId = this.channel()?.id ?? null;
    const reservedIdsInUse = new Set(
      this.existingChannels()
        .filter((channel) => channel.isReserved && channel.id !== currentChannelId)
        .map((channel) => channel.id)
    );

    return this.reservedChannels().filter((channel) => !reservedIdsInUse.has(channel.id));
  });

  constructor() {
    effect(() => {
      const incomingChannel = this.channel();
      const channelKind: ChannelKind = incomingChannel?.isReserved ? 'reserved' : 'custom';

      this.kind.set(channelKind);
      this.form.patchValue(
        {
          reservedChannelId: incomingChannel?.isReserved ? incomingChannel.id : '',
          name: incomingChannel?.name ?? '',
          abbreviation: incomingChannel?.abbreviation ?? '',
          dataType: this.normalizeDataType(incomingChannel?.dataType),
          baseUnitType: incomingChannel?.baseUnitType ?? '',
          baseDecimalPlaces: incomingChannel?.baseDecimalPlaces ?? 1,
          outputUnitType: incomingChannel?.outputUnitType ?? '',
          outputDecimalPlaces: incomingChannel?.outputDecimalPlaces ?? 1,
          category: incomingChannel?.category ?? '',
          groupTag: incomingChannel?.groupTag ?? '',
          enumConversion: incomingChannel?.enumConversion ?? '',
          lowRange: incomingChannel?.lowRange ?? 0,
          highRange: incomingChannel?.highRange ?? 100,
          defaultValue: incomingChannel?.defaultValue ?? 0,
          timeoutMs: incomingChannel?.timeoutMs ?? 0
        },
        { emitEvent: false }
      );

      if (channelKind === 'reserved') {
        this.ensureReservedChannelsLoaded();
      }

      this.syncDisabledState();
      this.loadAvailableUnitTypes();
    });

    this.form.controls.dataType.valueChanges.subscribe(() => {
      this.loadAvailableUnitTypes();
    });

  }

  async ngOnInit(): Promise<void> {
    if (this.kind() === 'reserved') {
      await this.ensureReservedChannelsLoaded();
    }
  }

  async onKindChange(event: Event): Promise<void> {
    const target = event.target as HTMLInputElement;
    const nextKind: ChannelKind = target.value === 'reserved' ? 'reserved' : 'custom';
    this.kind.set(nextKind);

    this.syncDisabledState();

    if (nextKind === 'reserved') {
      await this.ensureReservedChannelsLoaded();

      if (!this.form.controls.reservedChannelId.value) {
        const firstAvailable = this.availableReservedChannels()[0];
        if (firstAvailable) {
          this.form.controls.reservedChannelId.setValue(firstAvailable.id);
          this.applyReservedChannelToForm(firstAvailable.id);
        }
      }
      return;
    }

    this.form.controls.reservedChannelId.setValue('');
  }

  onReservedChannelChange(reservedChannelId: string): void {
    this.form.controls.reservedChannelId.setValue(reservedChannelId);
    this.applyReservedChannelToForm(reservedChannelId);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const isReserved = this.kind() === 'reserved';
    const selectedReservedChannelId = this.form.controls.reservedChannelId.value;
    const selectedReservedChannel = this.availableReservedChannels().find((channel) => channel.id === selectedReservedChannelId) ?? null;

    if (isReserved && !selectedReservedChannelId) {
      this.reservedLoadError.set('Select a reserved channel before saving.');
      return;
    }

    const dataType = this.form.controls.dataType.value;
    const existingChannel = this.channel();

    const channel: ChannelDefinition = {
      id: isReserved
        ? selectedReservedChannelId
        : (existingChannel?.isReserved ? createGuid() : existingChannel?.id || createGuid()),
      isReserved,
      category: this.form.controls.category.value.trim(),
      name: this.form.controls.name.value.trim(),
      abbreviation: this.form.controls.abbreviation.value.trim(),
      dataType,
      baseUnitType: this.form.controls.baseUnitType.value,
      baseDecimalPlaces: Number(this.form.controls.baseDecimalPlaces.value),
      outputUnitType: this.form.controls.outputUnitType.value,
      outputDecimalPlaces: Number(this.form.controls.outputDecimalPlaces.value),
      lowRange: Number(this.form.controls.lowRange.value),
      highRange: Number(this.form.controls.highRange.value),
      defaultValue: Number(this.form.controls.defaultValue.value),
      groupTag: this.form.controls.groupTag.value.trim(),
      enumConversion: this.form.controls.enumConversion.value || null,
      timeoutMs: Number(this.form.controls.timeoutMs.value)
    };

    if (isReserved && selectedReservedChannel) {
      channel.name = selectedReservedChannel.name;
      channel.abbreviation = selectedReservedChannel.abbreviation;
      channel.category = selectedReservedChannel.category;
      channel.dataType = this.normalizeDataType(selectedReservedChannel.dataType);
      channel.baseUnitType = selectedReservedChannel.baseUnitType;
      channel.baseDecimalPlaces = selectedReservedChannel.baseDecimalPlaces;
      channel.outputUnitType = selectedReservedChannel.outputUnitType;
      channel.outputDecimalPlaces = selectedReservedChannel.outputDecimalPlaces;
      channel.lowRange = selectedReservedChannel.lowRange;
      channel.highRange = selectedReservedChannel.highRange;
      channel.defaultValue = selectedReservedChannel.defaultValue;
      channel.timeoutMs = selectedReservedChannel.timeoutMs;

      channel.groupTag = selectedReservedChannel.groupTag;
      channel.enumConversion = selectedReservedChannel.enumConversion;
      channel.timeoutMs = selectedReservedChannel.timeoutMs;
    }

    this.save.emit(channel);
  }

  dismiss(): void {
    this.cancel.emit();
  }

  copyBaseToOutput(): void {
    this.form.controls.outputUnitType.setValue(this.form.controls.baseUnitType.value);
    this.form.controls.outputDecimalPlaces.setValue(this.form.controls.baseDecimalPlaces.value);
  }

  private syncDisabledState(): void {
    const opts = { emitEvent: false };
    if (this.kind() === 'reserved') {
      this.form.controls.dataType.disable(opts);
      this.form.controls.baseUnitType.disable(opts);
      this.form.controls.outputUnitType.disable(opts);
    } else {
      this.form.controls.dataType.enable(opts);
      this.form.controls.baseUnitType.enable(opts);
      this.form.controls.outputUnitType.enable(opts);
    }
  }

  private async ensureReservedChannelsLoaded(): Promise<void> {
    if (this.reservedChannels().length > 0 || this.loadingReserved()) {
      return;
    }

    this.loadingReserved.set(true);
    this.reservedLoadError.set(null);

    try {
      this.reservedChannels.set(await this.managementDataClient.loadReservedChannelDefinitionsAsync());
    } catch {
      this.reservedLoadError.set('Unable to load reserved channels.');
      this.reservedChannels.set([]);
    } finally {
      this.loadingReserved.set(false);
    }
  }

  private applyReservedChannelToForm(reservedChannelId: string): void {
    const selected = this.availableReservedChannels().find((channel) => channel.id === reservedChannelId);
    if (!selected) {
      return;
    }

    this.form.patchValue({
      name: selected.name,
      abbreviation: selected.abbreviation,
      dataType: this.normalizeDataType(selected.dataType),
      baseUnitType: selected.baseUnitType,
      baseDecimalPlaces: selected.baseDecimalPlaces,
      outputUnitType: selected.outputUnitType,
      outputDecimalPlaces: selected.outputDecimalPlaces,
      category: selected.category,
      groupTag: selected.groupTag,
      lowRange: selected.lowRange,
      highRange: selected.highRange,
      defaultValue: selected.defaultValue,
      timeoutMs: selected.timeoutMs
    });

    this.loadAvailableUnitTypes();
  }

  private normalizeDataType(value: string | undefined): ChannelDataType {
    const match = this.dataTypeOptions.find((option) => option.toLowerCase() === (value ?? '').toLowerCase());
    return match ?? 'Temperature';
  }

  private loadAvailableUnitTypes(): void {
    const selectedDataType = this.form.controls.dataType.value;

    if (selectedDataType === 'Unitless' || selectedDataType === 'String') {
      this.availableUnitTypes.set([]);
      this.unitTypesLoadError.set(null);
      return;
    }

    this.loadingUnitTypes.set(true);
    this.unitTypesLoadError.set(null);

    void this.managementDataClient
      .loadAvailableUnitTypesAsync(selectedDataType)
      .then((unitTypes) => {
        this.availableUnitTypes.set(unitTypes);
      })
      .catch(() => {
        this.availableUnitTypes.set([]);
        this.unitTypesLoadError.set('Unable to load unit types.');
      })
      .finally(() => {
        this.loadingUnitTypes.set(false);
      });
  }

}

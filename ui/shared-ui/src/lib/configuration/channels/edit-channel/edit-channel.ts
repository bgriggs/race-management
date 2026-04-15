import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MANAGEMENT_DATA_CLIENT } from '../../../data/management-data-client';
import { ChannelDefinition } from '../../../../models/channel-definition';

type ChannelKind = 'reserved' | 'custom';
type ChannelDataType =
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
  imports: [CommonModule, ReactiveFormsModule, MatSlideToggleModule],
  templateUrl: './edit-channel.html',
  styleUrl: './edit-channel.css',
})
export class EditChannel implements OnInit {
  private readonly managementDataClient = inject(MANAGEMENT_DATA_CLIENT);

  readonly channel = input<ChannelDefinition | null>(null);
  readonly existingChannels = input<ChannelDefinition[]>([]);

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
    isStringValue: new FormControl(false, { nonNullable: true }),
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
    lowRange: new FormControl(0, { nonNullable: true }),
    highRange: new FormControl(100, { nonNullable: true })
  });

  isStringDataType(): boolean {
    return this.form.controls.isStringValue.value;
  }

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
          isStringValue: incomingChannel?.isStringValue ?? false,
          dataType: this.normalizeDataType(incomingChannel?.dataType),
          baseUnitType: incomingChannel?.baseUnitType ?? '',
          baseDecimalPlaces: incomingChannel?.baseDecimalPlaces ?? 1,
          outputUnitType: incomingChannel?.outputUnitType ?? '',
          outputDecimalPlaces: incomingChannel?.outputDecimalPlaces ?? 1,
          category: incomingChannel?.category ?? '',
          groupTag: incomingChannel?.groupTag ?? '',
          lowRange: incomingChannel?.lowRange ?? 0,
          highRange: incomingChannel?.highRange ?? 100
        },
        { emitEvent: false }
      );

      if (channelKind === 'reserved') {
        this.ensureReservedChannelsLoaded();
      }

      this.loadAvailableUnitTypes();
    });

    this.form.controls.dataType.valueChanges.subscribe(() => {
      this.loadAvailableUnitTypes();
    });

    this.form.controls.isStringValue.valueChanges.subscribe((isStringValue) => {
      if (isStringValue) {
        this.availableUnitTypes.set([]);
        this.unitTypesLoadError.set(null);
        return;
      }

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
    const isStringValue = this.form.controls.isStringValue.value;
    const existingChannel = this.channel();

    const channel: ChannelDefinition = {
      id: isReserved
        ? selectedReservedChannelId
        : (existingChannel?.isReserved ? this.createGuid() : existingChannel?.id || this.createGuid()),
      isReserved,
      category: this.form.controls.category.value.trim(),
      name: this.form.controls.name.value.trim(),
      abbreviation: this.form.controls.abbreviation.value.trim(),
      dataType,
      isStringValue,
      baseUnitType: isStringValue ? '' : this.form.controls.baseUnitType.value,
      baseDecimalPlaces: isStringValue ? 0 : Number(this.form.controls.baseDecimalPlaces.value),
      outputUnitType: isStringValue ? '' : this.form.controls.outputUnitType.value,
      outputDecimalPlaces: isStringValue ? 0 : Number(this.form.controls.outputDecimalPlaces.value),
      lowRange: isStringValue ? 0 : Number(this.form.controls.lowRange.value),
      highRange: isStringValue ? 0 : Number(this.form.controls.highRange.value),
      groupTag: this.form.controls.groupTag.value.trim()
    };

    if (isReserved && selectedReservedChannel) {
      channel.name = selectedReservedChannel.name;
      channel.abbreviation = selectedReservedChannel.abbreviation;
      channel.category = selectedReservedChannel.category;
      channel.dataType = this.normalizeDataType(selectedReservedChannel.dataType);
      channel.isStringValue = selectedReservedChannel.isStringValue;
      channel.baseUnitType = channel.isStringValue ? '' : selectedReservedChannel.baseUnitType;
      channel.baseDecimalPlaces = channel.isStringValue ? 0 : selectedReservedChannel.baseDecimalPlaces;
      channel.outputUnitType = channel.isStringValue ? '' : selectedReservedChannel.outputUnitType;
      channel.outputDecimalPlaces = channel.isStringValue ? 0 : selectedReservedChannel.outputDecimalPlaces;
      channel.lowRange = channel.isStringValue ? 0 : selectedReservedChannel.lowRange;
      channel.highRange = channel.isStringValue ? 0 : selectedReservedChannel.highRange;
      channel.groupTag = selectedReservedChannel.groupTag;
    }

    this.save.emit(channel);
  }

  dismiss(): void {
    this.cancel.emit();
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
      isStringValue: selected.isStringValue,
      dataType: this.normalizeDataType(selected.dataType),
      baseUnitType: selected.baseUnitType,
      baseDecimalPlaces: selected.baseDecimalPlaces,
      outputUnitType: selected.outputUnitType,
      outputDecimalPlaces: selected.outputDecimalPlaces,
      category: selected.category,
      groupTag: selected.groupTag,
      lowRange: selected.lowRange,
      highRange: selected.highRange
    });

    this.loadAvailableUnitTypes();
  }

  private normalizeDataType(value: string | undefined): ChannelDataType {
    const match = this.dataTypeOptions.find((option) => option.toLowerCase() === (value ?? '').toLowerCase());
    return match ?? 'Temperature';
  }

  private loadAvailableUnitTypes(): void {
    const isStringValue = this.form.controls.isStringValue.value;
    if (isStringValue) {
      this.availableUnitTypes.set([]);
      this.unitTypesLoadError.set(null);
      return;
    }

    const selectedDataType = this.form.controls.dataType.value;
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

  private createGuid(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (character) => {
      const randomNibble = Math.floor(Math.random() * 16);
      const value = character === 'x' ? randomNibble : (randomNibble & 0x3) | 0x8;
      return value.toString(16);
    });
  }

}

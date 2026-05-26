import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MANAGEMENT_DATA_CLIENT } from '../../../data/management-data-client';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ChannelDistribution } from '../../../../models/channel-distribution';
import { ChannelScope } from '../../../../models/channel-scope';
import { createGuid } from '../../../utils/guid';
import { EnumDefinition } from '../../../../models/enum-definition';

type ChannelKind = 'reserved' | 'custom';
type ChannelOrigin = 'Car' | 'Cloud';
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
  | 'ElectricPotential'
  | 'Mass'
  | 'Ratio'
  | 'ElectricCurrent'
  | 'ElectricResistance';

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

  readonly kind = signal<ChannelKind>('reserved');
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
    'ElectricPotential',
    'Mass',
    'Ratio',
    'ElectricCurrent',
    'ElectricResistance'
  ];

  readonly form = new FormGroup({
    reservedChannelId: new FormControl('', { nonNullable: true }),
    name: new FormControl('', {
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(25)],
      nonNullable: true
    }),
    abbreviation: new FormControl('', {
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(6)],
      nonNullable: true
    }),
    dataType: new FormControl<ChannelDataType>('Temperature', { nonNullable: true }),
    baseUnitType: new FormControl('', { nonNullable: true }),
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
    timeoutMs: new FormControl(0, { nonNullable: true }),
    distribution: new FormControl<ChannelDistribution>(ChannelDistribution.CarToCloud, { nonNullable: true }),
    scope: new FormControl<ChannelScope>(ChannelScope.PerCar, { nonNullable: true })
  }, { validators: EditChannel.defaultValueInRangeValidator });

  // Expose enums for template-level [value] bindings.
  protected readonly ChannelDistribution = ChannelDistribution;
  protected readonly ChannelScope = ChannelScope;

  // Origin selector for new custom channels. Hidden when editing an existing channel
  // (origin is fixed at creation per ADR-0007 amendment 2026-05-25).
  readonly originForCreate = signal<ChannelOrigin>('Car');

  // Tracks the form's current distribution value so dependent computed signals react.
  // valueChanges with { emitEvent: false } does not fire, so this is updated manually
  // wherever we patchValue silently (the channel-input reset effect).
  private readonly currentDistribution = signal<ChannelDistribution>(ChannelDistribution.CarToCloud);

  // Origin is derived from the current distribution for existing channels, and from the
  // Origin radio for new custom channels (where there is no prior distribution to read).
  readonly origin = computed<ChannelOrigin>(() => {
    if (this.kind() === 'custom' && !this.channel()) {
      return this.originForCreate();
    }
    return EditChannel.originFor(this.currentDistribution());
  });

  // The 2 distribution options matching the channel's origin. The dropdown never offers
  // an origin-crossing option — origin is fixed post-create.
  readonly distributionOptions = computed<Array<{ value: ChannelDistribution; label: string }>>(() =>
    this.origin() === 'Car'
      ? [
          { value: ChannelDistribution.CarToCloud, label: 'Car to Cloud' },
          { value: ChannelDistribution.CarLocal, label: 'Car Local (no cloud)' }
        ]
      : [
          { value: ChannelDistribution.CloudToCar, label: 'Cloud to Car' },
          { value: ChannelDistribution.CloudLocal, label: 'Cloud Local (no car)' }
        ]
  );

  // Origin radio is only meaningful when creating a new custom channel.
  readonly canChooseOrigin = computed(() => this.kind() === 'custom' && !this.channel());

  // True when the current channel's distribution is pinned by the reserved template
  // (e.g., ThrottleProxy* outputs whose feature genuinely requires CarToCloud).
  readonly isDistributionLocked = computed(() => this.channel()?.isDistributionLocked === true);

  private static originFor(distribution: ChannelDistribution): ChannelOrigin {
    return distribution === ChannelDistribution.CarLocal || distribution === ChannelDistribution.CarToCloud
      ? 'Car'
      : 'Cloud';
  }

  static defaultValueInRangeValidator(group: AbstractControl): ValidationErrors | null {
    const low = Number((group as FormGroup).controls['lowRange'].value);
    const high = Number((group as FormGroup).controls['highRange'].value);
    const def = Number((group as FormGroup).controls['defaultValue'].value);
    if (isNaN(low) || isNaN(high) || isNaN(def)) return null;
    return def >= low && def <= high ? null : { defaultValueOutOfRange: true };
  }

  private readonly preferredUnitTypesByDataType: Partial<Record<ChannelDataType, string[]>> = {
    Temperature:  ['DegreeFahrenheit', 'DegreeCelsius', 'Kelvin'],
    Length:       ['Meter', 'Kilometer', 'Foot', 'Inch', 'Mile'],
    Volume:       ['UsGallon','Liter', 'Milliliter', 'CubicMeter'],
    VolumeFlow:   ['LiterPerMinute', 'UsGallonPerMinute', 'LiterPerSecond', 'MilliliterPerMinute', 'CubicMeterPerSecond'],
    Duration:     ['Second', 'Minute', 'Hour', 'Millisecond'],
    Speed:        ['MilePerHour', 'KilometerPerHour', 'MeterPerSecond', 'FootPerSecond'],
    Pressure:     ['PoundForcePerSquareInch', 'Bar', 'Kilopascal', 'Megapascal', 'Pascal'],
    Force:        ['PoundForce', 'Newton', 'KilogramForce'],
    ElectricPotential: ['Volt', 'Millivolt', 'Microvolt'],
    Mass:         ['Pound','Kilogram', 'Gram', 'Ounce'],
    Ratio:        ['Percent', 'DecimalFraction', 'PartsPerMillion', 'PartsPerBillion', 'PartsPerThousand'],
    ElectricCurrent:    ['Ampere', 'Milliampere'],
    ElectricResistance: ['Ohm'],
  };

  readonly commonUnitTypes = computed(() => {
    const preferred = this.preferredUnitTypesByDataType[this.dataTypeValue()] ?? [];
    const available = new Set(this.availableUnitTypes());
    return preferred.filter(u => available.has(u));
  });

  readonly otherUnitTypes = computed(() => {
    const common = new Set(this.commonUnitTypes());
    return this.availableUnitTypes().filter(u => !common.has(u));
  });

  readonly toDisplayName = (name: string): string =>
    name
      .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
      .replace(/([a-z])([A-Z])/g, '$1 $2');

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

    // Hide managed reserved templates from the picker (users get them via feature toggle),
    // BUT keep the currently-edited channel in the list so its <option> exists in the
    // Name <select> and the bound reservedChannelId selects correctly.
    return this.reservedChannels()
      .filter((channel) =>
        !reservedIdsInUse.has(channel.id)
        && (!channel.managedByFeature || channel.id === currentChannelId))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  // True when the editor is bound to an existing managed reserved channel. The Name <select>
  // is disabled in this case so the user can't accidentally swap the managed channel for an
  // unmanaged one (which would strip ManagedByFeature and break the feature toggle's lifecycle).
  readonly isEditingManagedChannel = computed(() => {
    const ch = this.channel();
    return ch != null && ch.isReserved && !!ch.managedByFeature;
  });

  private lastResetChannelId: string | null | undefined = undefined;
  constructor() {
    effect(() => {
      const incomingChannel = this.channel();
      const incomingChannelId = incomingChannel?.id ?? null;

      if (this.lastResetChannelId === incomingChannelId) {
        return;
      }
      this.lastResetChannelId = incomingChannelId;

      untracked(() => {
        const channelKind: ChannelKind = !incomingChannel || incomingChannel.isReserved ? 'reserved' : 'custom';

        this.kind.set(channelKind);
        this.form.patchValue(
          {
            reservedChannelId: incomingChannel?.isReserved ? incomingChannel.id : '',
            name: incomingChannel?.name ?? '',
            abbreviation: incomingChannel?.abbreviation ?? '',
            dataType: this.normalizeDataType(incomingChannel?.dataType),
            baseUnitType: incomingChannel?.baseUnitType ?? '',
            outputUnitType: incomingChannel?.outputUnitType ?? '',
            outputDecimalPlaces: incomingChannel?.outputDecimalPlaces ?? 1,
            category: incomingChannel?.category ?? '',
            groupTag: incomingChannel?.groupTag ?? '',
            enumConversion: incomingChannel?.enumConversion ?? '',
            lowRange: incomingChannel?.lowRange ?? 0,
            highRange: incomingChannel?.highRange ?? 100,
            defaultValue: incomingChannel?.defaultValue ?? 0,
            timeoutMs: incomingChannel?.timeoutMs ?? 0,
            distribution: incomingChannel?.distribution ?? ChannelDistribution.CarToCloud,
            scope: incomingChannel?.scope ?? ChannelScope.PerCar
          },
          { emitEvent: false }
        );
        // patchValue with { emitEvent: false } skips valueChanges, so mirror the new
        // distribution into the signal that drives origin/distributionOptions/isDistributionLocked.
        this.currentDistribution.set(incomingChannel?.distribution ?? ChannelDistribution.CarToCloud);

        if (channelKind === 'reserved') {
          this.ensureReservedChannelsLoaded();
        }

        this.syncDisabledState();
        this.loadAvailableUnitTypes();
      });
    });

    this.form.controls.dataType.valueChanges.subscribe(() => {
      this.loadAvailableUnitTypes();
    });

    this.form.controls.distribution.valueChanges.subscribe((value) => {
      this.currentDistribution.set(value);
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
    // Reset distribution to the default for the currently-selected Origin radio; scope is locked to PerCar.
    this.form.controls.distribution.setValue(EditChannel.defaultDistributionFor(this.originForCreate()));
    this.form.controls.scope.setValue(ChannelScope.PerCar);
  }

  onOriginChange(origin: ChannelOrigin): void {
    this.originForCreate.set(origin);
    // Reset distribution to the chosen origin's default (transmit-by-default for Car;
    // local-only for Cloud, since most new cloud channels start without a need to push to the car).
    this.form.controls.distribution.setValue(EditChannel.defaultDistributionFor(origin));
  }

  private static defaultDistributionFor(origin: ChannelOrigin): ChannelDistribution {
    return origin === 'Car' ? ChannelDistribution.CarToCloud : ChannelDistribution.CloudLocal;
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
      // baseDecimalPlaces removed
      outputUnitType: this.form.controls.outputUnitType.value,
      outputDecimalPlaces: Number(this.form.controls.outputDecimalPlaces.value),
      lowRange: Number(this.form.controls.lowRange.value),
      highRange: Number(this.form.controls.highRange.value),
      defaultValue: Number(this.form.controls.defaultValue.value),
      groupTag: this.form.controls.groupTag.value.trim(),
      enumConversion: this.form.controls.enumConversion.value || null,
      timeoutMs: Number(this.form.controls.timeoutMs.value),
      distribution: this.form.controls.distribution.value,
      isDistributionLocked: false,
      scope: this.form.controls.scope.value,
      managedByFeature: null,
      producedByFeature: null
    };

    if (isReserved && selectedReservedChannel) {
      channel.name = selectedReservedChannel.name;
      channel.abbreviation = selectedReservedChannel.abbreviation;
      channel.category = selectedReservedChannel.category;
      channel.dataType = this.normalizeDataType(selectedReservedChannel.dataType);
      channel.groupTag = selectedReservedChannel.groupTag;
      channel.enumConversion = selectedReservedChannel.enumConversion;
      channel.scope = selectedReservedChannel.scope;
      channel.managedByFeature = selectedReservedChannel.managedByFeature;
      // Propagate the template's lock state; if true the server rejects any distribution change.
      channel.isDistributionLocked = selectedReservedChannel.isDistributionLocked;
      channel.producedByFeature = selectedReservedChannel.producedByFeature;
    } else if (existingChannel?.isReserved) {
      // Editing an existing reserved channel — preserve the lock + producer from the persisted record.
      channel.isDistributionLocked = existingChannel.isDistributionLocked;
      channel.producedByFeature = existingChannel.producedByFeature;
    }

    this.save.emit(channel);
  }

  dismiss(): void {
    this.cancel.emit();
  }

  copyBaseToOutput(): void {
    this.form.controls.outputUnitType.setValue(this.form.controls.baseUnitType.value);
    // baseDecimalPlaces removed from copyBaseToOutput
  }

  private syncDisabledState(): void {
    const opts = { emitEvent: false };
    if (this.kind() === 'reserved') {
      this.form.controls.dataType.disable(opts);
      this.form.controls.baseUnitType.enable(opts);
      this.form.controls.outputUnitType.enable(opts);
      this.form.controls.scope.disable(opts);
    } else {
      this.form.controls.dataType.enable(opts);
      this.form.controls.baseUnitType.enable(opts);
      this.form.controls.outputUnitType.enable(opts);
      // Custom channels are locked to PerCar scope in v1 (ADR-0007).
      this.form.controls.scope.disable(opts);
    }

    // Distribution editability is uniform: locked iff the channel template pins it
    // (currently only the ThrottleProxy* outputs).
    if (this.channel()?.isDistributionLocked === true) {
      this.form.controls.distribution.disable(opts);
    } else {
      this.form.controls.distribution.enable(opts);
    }

    // Lock the Name dropdown when editing a managed channel — swapping its identity would
    // strip ManagedByFeature and leave the feature's lifecycle handler dangling.
    if (this.isEditingManagedChannel()) {
      this.form.controls.reservedChannelId.disable(opts);
    } else {
      this.form.controls.reservedChannelId.enable(opts);
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
      // baseDecimalPlaces removed
      outputUnitType: selected.outputUnitType,
      outputDecimalPlaces: selected.outputDecimalPlaces,
      category: selected.category,
      groupTag: selected.groupTag,
      lowRange: selected.lowRange,
      highRange: selected.highRange,
      defaultValue: selected.defaultValue,
      timeoutMs: selected.timeoutMs,
      distribution: selected.distribution,
      scope: selected.scope
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

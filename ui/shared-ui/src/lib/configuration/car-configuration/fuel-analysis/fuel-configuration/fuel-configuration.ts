import { Component, effect, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { CarFuelConfig } from '../../../../../models/car-fuel-config';
import { ThrottleConsumptionConfig } from '../../../../../models/throttle-consumption-config';
import { ChannelDefinition } from '../../../../../models/channel-definition';
import { ChannelSelector } from '../../../channels/channel-selector/channel-selector';

// Reserved-channel GUID defaults — must stay in sync with CarFuelConfig.cs and ThrottleConsumptionConfig.cs.
const FUEL_LEVEL_DEFAULT = 'a2529acf-a7c6-449f-8a85-c7d76b35dbcb';
const TRIP_FUEL_DEFAULT = 'acd3d127-acaf-4f8a-b27a-8623cfda09f3';
const FUEL_USED_DEFAULT = '740ce2a6-dc88-4425-85dc-7f99f2a902f1';
const FUEL_FULL_DEFAULT = 'c3b94831-95f6-4935-bf67-1aacfd611f75';
const IN_PIT_DEFAULT = 'da12563a-1167-4899-9956-700b0b693005';

// Fallback used when constructing a throttle-consumption payload from a fuel config that has
// no nested throttle-consumption yet (first save before the user has opened that editor).
const DEFAULT_THROTTLE_CONSUMPTION: ThrottleConsumptionConfig = {
  isEnabled: false,
  maxRpm: 7000,
  throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
  engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
};

@Component({
  selector: 'lib-fuel-configuration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSlideToggleModule, ChannelSelector],
  templateUrl: './fuel-configuration.html',
  styleUrl: './fuel-configuration.css',
})
export class FuelConfiguration {
  readonly configuration = input<CarFuelConfig | null>(null);
  readonly channels = input<ChannelDefinition[]>([]);
  readonly usedChannelIds = input<string[]>([]);
  readonly configurationChange = output<CarFuelConfig>();

  readonly form = new FormGroup({
    isEnabled: new FormControl(false, { nonNullable: true }),
    tankCapacityGallons: new FormControl(0, {
      validators: [Validators.required, Validators.min(1), Validators.max(100)],
      nonNullable: true,
    }),
    defaultConsumptionGalPerMin: new FormControl(0, {
      validators: [Validators.required, Validators.min(0.0001), Validators.max(10)],
      nonNullable: true,
    }),
    defaultYellowConsumptionMultiplier: new FormControl(0.5, {
      validators: [Validators.required, Validators.min(0), Validators.max(1)],
      nonNullable: true,
    }),
    defaultCode35ConsumptionMultiplier: new FormControl(0.3, {
      validators: [Validators.required, Validators.min(0), Validators.max(1)],
      nonNullable: true,
    }),
    // Fuel-signal bindings are optional at the form level — the reconciler picks whichever
    // estimators have their inputs ready (tank-level, cumulative, ECU-reset-aware, etc.) so
    // any subset can be configured. Defaults point at the reserved channels so doing nothing
    // still yields working baseline behavior.
    fuelLevelChannelId: new FormControl(FUEL_LEVEL_DEFAULT, { nonNullable: true }),
    tripFuelChannelId: new FormControl(TRIP_FUEL_DEFAULT, { nonNullable: true }),
    fuelUsedChannelId: new FormControl(FUEL_USED_DEFAULT, { nonNullable: true }),
    fuelFullChannelId: new FormControl(FUEL_FULL_DEFAULT, { nonNullable: true }),
    inPitChannelId: new FormControl<string | null>(IN_PIT_DEFAULT),
    throttleConsumptionEnabled: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const config = this.configuration();
      this.form.patchValue(
        {
          isEnabled: config?.isEnabled ?? false,
          tankCapacityGallons: config?.tankCapacityGallons ?? 0,
          defaultConsumptionGalPerMin: config?.defaultConsumptionGalPerMin ?? 0,
          defaultYellowConsumptionMultiplier: config?.defaultYellowConsumptionMultiplier ?? 0.5,
          defaultCode35ConsumptionMultiplier: config?.defaultCode35ConsumptionMultiplier ?? 0.3,
          fuelLevelChannelId: config?.fuelLevelChannelId ?? FUEL_LEVEL_DEFAULT,
          tripFuelChannelId: config?.tripFuelChannelId ?? TRIP_FUEL_DEFAULT,
          fuelUsedChannelId: config?.fuelUsedChannelId ?? FUEL_USED_DEFAULT,
          fuelFullChannelId: config?.fuelFullChannelId ?? FUEL_FULL_DEFAULT,
          inPitChannelId: config?.inPitChannelId ?? IN_PIT_DEFAULT,
          throttleConsumptionEnabled: config?.throttleConsumption?.isEnabled ?? false,
        },
        { emitEvent: false }
      );
    });

    this.form.valueChanges.subscribe(() => {
      const current = this.configuration();
      this.configurationChange.emit({
        isEnabled: this.form.controls.isEnabled.value,
        tankCapacityGallons: this.form.controls.tankCapacityGallons.value,
        defaultConsumptionGalPerMin: this.form.controls.defaultConsumptionGalPerMin.value,
        defaultYellowConsumptionMultiplier: this.form.controls.defaultYellowConsumptionMultiplier.value,
        defaultCode35ConsumptionMultiplier: this.form.controls.defaultCode35ConsumptionMultiplier.value,
        fuelLevelChannelId: this.form.controls.fuelLevelChannelId.value,
        tripFuelChannelId: this.form.controls.tripFuelChannelId.value,
        fuelUsedChannelId: this.form.controls.fuelUsedChannelId.value,
        fuelFullChannelId: this.form.controls.fuelFullChannelId.value,
        inPitChannelId: this.form.controls.inPitChannelId.value,
        // Preserve throttle-consumption (incl. its own input channel IDs); only isEnabled is owned by this form.
        throttleConsumption: {
          ...DEFAULT_THROTTLE_CONSUMPTION,
          ...current?.throttleConsumption,
          isEnabled: this.form.controls.throttleConsumptionEnabled.value,
        },
      });
    });
  }

  onChannelChange(controlName: 'fuelLevelChannelId' | 'tripFuelChannelId' | 'fuelUsedChannelId' | 'fuelFullChannelId', channelId: string | null): void {
    this.form.controls[controlName].setValue(channelId ?? '');
  }

  onInPitChannelChange(channelId: string | null): void {
    this.form.controls.inPitChannelId.setValue(channelId);
  }
}

import { Component, effect, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { CarFuelConfig } from '../../../../../models/car-fuel-config';

@Component({
  selector: 'lib-fuel-configuration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSlideToggleModule],
  templateUrl: './fuel-configuration.html',
  styleUrl: './fuel-configuration.css',
})
export class FuelConfiguration {
  readonly configuration = input<CarFuelConfig | null>(null);
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
        throttleConsumption: {
          isEnabled: this.form.controls.throttleConsumptionEnabled.value,
          maxRpm: current?.throttleConsumption?.maxRpm ?? 7000,
        },
      });
    });
  }
}

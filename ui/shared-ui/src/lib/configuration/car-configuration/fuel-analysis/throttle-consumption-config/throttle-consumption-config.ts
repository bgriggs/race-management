import { Component, effect, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ThrottleConsumptionConfig as ThrottleConsumptionConfigModel } from '../../../../../models/throttle-consumption-config';

@Component({
  selector: 'lib-throttle-consumption-config',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './throttle-consumption-config.html',
  styleUrl: './throttle-consumption-config.css',
})
export class ThrottleConsumptionConfig {
  readonly configuration = input<ThrottleConsumptionConfigModel | null>(null);
  readonly configurationChange = output<ThrottleConsumptionConfigModel>();

  readonly form = new FormGroup({
    maxRpm: new FormControl(7000, {
      validators: [Validators.required, Validators.min(2000), Validators.max(12000)],
      nonNullable: true,
    }),
  });

  constructor() {
    effect(() => {
      const config = this.configuration();
      this.form.patchValue(
        { maxRpm: config?.maxRpm ?? 7000 },
        { emitEvent: false }
      );
    });

    this.form.valueChanges.subscribe(() => {
      const current = this.configuration();
      this.configurationChange.emit({
        isEnabled: current?.isEnabled ?? true,
        maxRpm: this.form.controls.maxRpm.value,
      });
    });
  }
}

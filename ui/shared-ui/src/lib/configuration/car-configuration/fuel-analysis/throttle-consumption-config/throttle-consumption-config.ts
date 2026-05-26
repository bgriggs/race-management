import { Component, effect, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ThrottleConsumptionConfig as ThrottleConsumptionConfigModel } from '../../../../../models/throttle-consumption-config';
import { ChannelDefinition } from '../../../../../models/channel-definition';
import { ChannelSelector } from '../../../channels/channel-selector/channel-selector';

const THROTTLE_POSITION_DEFAULT = 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01';
const ENGINE_RPM_DEFAULT = '74c57a58-d78d-499a-977b-11cee221926a';

@Component({
  selector: 'lib-throttle-consumption-config',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ChannelSelector],
  templateUrl: './throttle-consumption-config.html',
  styleUrl: './throttle-consumption-config.css',
})
export class ThrottleConsumptionConfig {
  readonly configuration = input<ThrottleConsumptionConfigModel | null>(null);
  readonly channels = input<ChannelDefinition[]>([]);
  readonly usedChannelIds = input<string[]>([]);
  readonly configurationChange = output<ThrottleConsumptionConfigModel>();

  readonly form = new FormGroup({
    maxRpm: new FormControl(7000, {
      validators: [Validators.required, Validators.min(2000), Validators.max(12000)],
      nonNullable: true,
    }),
    throttlePositionChannelId: new FormControl(THROTTLE_POSITION_DEFAULT, {
      validators: [Validators.required],
      nonNullable: true,
    }),
    engineRpmChannelId: new FormControl(ENGINE_RPM_DEFAULT, {
      validators: [Validators.required],
      nonNullable: true,
    }),
  });

  constructor() {
    effect(() => {
      const config = this.configuration();
      this.form.patchValue(
        {
          maxRpm: config?.maxRpm ?? 7000,
          throttlePositionChannelId: config?.throttlePositionChannelId ?? THROTTLE_POSITION_DEFAULT,
          engineRpmChannelId: config?.engineRpmChannelId ?? ENGINE_RPM_DEFAULT,
        },
        { emitEvent: false }
      );
    });

    this.form.valueChanges.subscribe(() => {
      const current = this.configuration();
      this.configurationChange.emit({
        isEnabled: current?.isEnabled ?? true,
        maxRpm: this.form.controls.maxRpm.value,
        throttlePositionChannelId: this.form.controls.throttlePositionChannelId.value,
        engineRpmChannelId: this.form.controls.engineRpmChannelId.value,
      });
    });
  }

  onChannelChange(controlName: 'throttlePositionChannelId' | 'engineRpmChannelId', channelId: string | null): void {
    this.form.controls[controlName].setValue(channelId ?? '');
  }
}

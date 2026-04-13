import { Component, effect, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CarConfiguration } from '../../../models/car-configuration';

@Component({
  selector: 'lib-cloud-configuration',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './cloud-configuration.html',
  styleUrl: './cloud-configuration.css',
})
export class CloudConfiguration {
  readonly configuration = input<Pick<CarConfiguration, 'clientId' | 'clientSecret'> | null>(null);
  readonly configurationChange = output<Pick<CarConfiguration, 'clientId' | 'clientSecret'>>();

  readonly form = new FormGroup({
    clientId: new FormControl('', {
      validators: [Validators.required, Validators.maxLength(64)],
      nonNullable: true
    }),
    clientSecret: new FormControl('', {
      validators: [Validators.required, Validators.maxLength(32)],
      nonNullable: true
    })
  });

  constructor() {
    effect(() => {
      const config = this.configuration();
      this.form.patchValue(
        {
          clientId: config?.clientId ?? '',
          clientSecret: config?.clientSecret ?? ''
        },
        { emitEvent: false }
      );
    });

    this.form.valueChanges.subscribe(() => {
      this.configurationChange.emit({
        clientId: this.form.controls.clientId.value,
        clientSecret: this.form.controls.clientSecret.value
      });
    });
  }

}

import { Component, effect, input, output } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CarConfiguration } from '../../../models/car-configuration';

@Component({
  selector: 'lib-general-settings',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './general-settings.html',
  styleUrl: './general-settings.css',
})
export class GeneralSettings {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly configurationChange = output<Pick<CarConfiguration, 'name' | 'car' | 'notes'>>();

  readonly form = new FormGroup({
    name: new FormControl('', {
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(32)],
      nonNullable: true
    }),
    car: new FormControl('', {
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(6)],
      nonNullable: true
    }),
    notes: new FormControl('', {
      validators: [Validators.maxLength(1024)],
      nonNullable: true
    })
  });

  constructor() {
    effect(() => {
      const config = this.configuration();
      this.form.patchValue(
        { name: config?.name ?? '', car: config?.car ?? '', notes: config?.notes ?? '' },
        { emitEvent: false }
      );
    });

    this.form.valueChanges.subscribe(() => {
      this.configurationChange.emit({
        name: this.form.controls.name.value,
        car: this.form.controls.car.value,
        notes: this.form.controls.notes.value
      });
    });
  }

  get configurationIdDisplay(): string {
    const id = this.configuration()?.configurationId;
    return id || '<new>';
  }

  get lastUpdatedDisplay(): string {
    const date = this.configuration()?.lastUpdated;
    if (!date) return '';
    const parsed = new Date(date);
    return isNaN(parsed.getTime()) ? '' : parsed.toLocaleString();
  }
}

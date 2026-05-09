import { Component, computed, model, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { EnumDefinition } from '../../../../models/enum-definition';

@Component({
  selector: 'lib-edit-enum',
  standalone: true,
  imports: [MatIcon],
  templateUrl: './edit-enum.html',
  styleUrl: './edit-enum.css',
})
export class EditEnum {
  readonly enumDefinition = model<EnumDefinition>(this.createEmptyEnum());
  readonly isNameDirty = signal(false);

  readonly isNameValid = computed(() => {
    const trimmedLength = this.enumDefinition().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });

  readonly areValuesValid = computed(() =>
    this.enumDefinition().values.every((value) =>
      Number.isInteger(value.value) && value.source.trim().length >= 1 && value.source.trim().length <= 10
    )
  );

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
    this.enumDefinition.set({
      ...this.enumDefinition(),
      name: value,
    });
  }

  addRow(): void {
    this.enumDefinition.set({
      ...this.enumDefinition(),
      values: [...this.enumDefinition().values, { value: 0, source: '' }],
    });
  }

  removeRow(index: number): void {
    this.enumDefinition.set({
      ...this.enumDefinition(),
      values: this.enumDefinition().values.filter((_, rowIndex) => rowIndex !== index),
    });
  }

  onNumericValueChanged(index: number, rawValue: string): void {
    const parsedValue = Number.parseInt(rawValue, 10);
    const value = Number.isNaN(parsedValue) ? 0 : parsedValue;
    const updatedValues = this.enumDefinition().values.map((row, rowIndex) =>
      rowIndex === index ? { ...row, value } : row
    );

    this.enumDefinition.set({
      ...this.enumDefinition(),
      values: updatedValues,
    });
  }

  onStringValueChanged(index: number, source: string): void {
    const updatedValues = this.enumDefinition().values.map((row, rowIndex) =>
      rowIndex === index ? { ...row, source } : row
    );

    this.enumDefinition.set({
      ...this.enumDefinition(),
      values: updatedValues,
    });
  }

  private createEmptyEnum(): EnumDefinition {
    const enumDefinition = new EnumDefinition();
    enumDefinition.id = '';
    enumDefinition.name = '';
    enumDefinition.values = [];
    return enumDefinition;
  }

}

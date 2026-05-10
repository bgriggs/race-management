import { Component, computed, input, model, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { TableDefinition } from '../../../../models/table-definition';
import { TableMapping } from '../../../../models/table-mapping';
import { InterpolationType } from '../../../../models/interpolation-type';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';

@Component({
  selector: 'lib-edit-table',
  imports: [MatIcon, ChannelSelector],
  templateUrl: './edit-table.html',
  styleUrl: './edit-table.css',
})
export class EditTable {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly table = model<TableDefinition>(this.createEmptyTable());

  readonly isNameDirty = signal(false);

  readonly isNameValid = computed(() => {
    const trimmedLength = this.table().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 50;
  });

  readonly interpolationTypes = [
    { value: InterpolationType.Linear, label: 'Linear' },
    { value: InterpolationType.CubicSpline, label: 'Cubic Spline' },
    { value: InterpolationType.Polynomial, label: 'Polynomial' },
  ];

  readonly rowCount = computed(() => this.table().mappings.length);

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
    this.table.set({ ...this.table(), name: value });
  }

  onInputChannelChanged(channelId: string | null): void {
    this.table.set({ ...this.table(), inputChannel: channelId ?? '' });
  }

  onOutputChannelChanged(channelId: string | null): void {
    this.table.set({ ...this.table(), outputChannel: channelId ?? '' });
  }

  onInterpolationTypeChanged(value: string): void {
    this.table.set({ ...this.table(), interpolationType: +value as InterpolationType });
  }

  addRow(): void {
    const t = this.table();
    this.table.set({
      ...t,
      mappings: [...t.mappings, { input: '0', output: '0' } satisfies TableMapping],
    });
  }

  removeRow(index: number): void {
    const t = this.table();
    this.table.set({
      ...t,
      mappings: t.mappings.filter((_, i) => i !== index),
    });
  }

  onInputPointChanged(index: number, rawValue: string): void {
    const mappings = this.table().mappings.map((row, i) =>
      i === index ? { ...row, input: rawValue } : row
    );
    this.table.set({ ...this.table(), mappings });
  }

  onOutputValueChanged(index: number, rawValue: string): void {
    const mappings = this.table().mappings.map((row, i) =>
      i === index ? { ...row, output: rawValue } : row
    );
    this.table.set({ ...this.table(), mappings });
  }

  private createEmptyTable(): TableDefinition {
    return {
      id: '',
      name: '',
      isEnum: false,
      ignoreCase: false,
      inputChannel: '',
      outputChannel: '',
      interpolationType: InterpolationType.Linear,
      mappings: [],
    };
  }
}

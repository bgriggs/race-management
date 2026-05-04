import { Component, computed, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { MathDefinition } from '../../../../models/math-definition';
import { MathType } from '../../../../models/math-type';
import { SimpleOperationType } from '../../../../models/simple-operation-type';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';

@Component({
  selector: 'lib-edit-math',
  standalone: true,
  imports: [ChannelSelector, FormsModule],
  templateUrl: './edit-math.html',
  styleUrl: './edit-math.css',
})
export class EditMath {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);
  readonly math = model.required<MathDefinition>();

  readonly isNameDirty = signal(false);
  readonly isNameValid = computed(() => {
    const trimmedLength = this.math().name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly isBias = computed(() => this.math().type === MathType.Bias);
  readonly isLinearCorrector = computed(() => this.math().type === MathType.LinearCorrector);
  readonly isSimpleOperation = computed(() => this.math().type === MathType.SimpleOperation);
  readonly isDivisionInteger = computed(() => this.math().type === MathType.DivisionInteger);
  readonly isDivisionModulo = computed(() => this.math().type === MathType.DivisionModulo);

  readonly showChannel2 = computed(() => this.isBias() || this.isSimpleOperation());
  readonly showA = computed(() =>
    this.isLinearCorrector() || this.isDivisionInteger() || this.isDivisionModulo()
  );
  readonly showB = computed(() => this.isLinearCorrector());

  readonly mathTypes = [
    { value: MathType.Bias, label: 'Bias - Output = CH1 / (CH1 + CH2)' },
    { value: MathType.LinearCorrector, label: 'Linear Corrector - Output = (A * CH1) + B' },
    { value: MathType.SimpleOperation, label: 'Simple Operation - Output = CH1 + CH2' },
    { value: MathType.DivisionInteger, label: 'Division Integer - Output = (int)(CH1 / A)' },
    { value: MathType.DivisionModulo, label: 'Division Modulo - Output = CH1 % A' },
  ] as const;

  readonly simpleOperationTypes = [
    { value: SimpleOperationType.Add, label: 'Add' },
    { value: SimpleOperationType.Subtract, label: 'Subtract' },
    { value: SimpleOperationType.Multiply, label: 'Multiply' },
    { value: SimpleOperationType.Divide, label: 'Divide' },
  ] as const;

  onNameChanged(value: string): void {
    this.isNameDirty.set(true);
    this.math.set({ ...this.math(), name: value });
  }

  onTypeChanged(value: number): void {
    this.math.set({ ...this.math(), type: value as MathType });
  }

  onAChanged(value: number): void {
    this.math.set({ ...this.math(), a: value });
  }

  onBChanged(value: number): void {
    this.math.set({ ...this.math(), b: value });
  }

  onChannel1Changed(channelId: string | null): void {
    this.math.set({ ...this.math(), channel1Id: channelId ?? '' });
  }

  onChannel2Changed(channelId: string | null): void {
    this.math.set({ ...this.math(), channel2Id: channelId ?? null });
  }

  onOutputChannelChanged(channelId: string | null): void {
    this.math.set({ ...this.math(), outputChannelId: channelId ?? '' });
  }

  onSimpleOperationTypeChanged(value: number): void {
    this.math.set({ ...this.math(), simpleOperationType: value as SimpleOperationType });
  }

}

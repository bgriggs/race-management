import { Component, computed, input, model } from '@angular/core';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ComparisonDefinition } from '../../../../models/comparison-definition';
import { LogicType } from '../../../../models/logic-type';
import { ChannelSelector } from '../../channels/channel-selector/channel-selector';

interface LogicOption {
  value: LogicType;
  label: string;
}

@Component({
  selector: 'lib-edit-comparison',
  standalone: true,
  imports: [ChannelSelector],
  templateUrl: './edit-comparison.html',
  styleUrl: './edit-comparison.css',
})
export class EditComparison {
  readonly channels = input.required<ChannelDefinition[]>();
  readonly usedChannelIds = input<string[]>([]);

  readonly comparison = model<ComparisonDefinition>(this.createEmptyComparison());

  readonly logicOptions: readonly LogicOption[] = [
    { value: LogicType.GreaterThan, label: 'Greater Than' },
    { value: LogicType.LessThan, label: 'Less Than' },
    { value: LogicType.GreaterThanOrEqualTo, label: 'Greater Than Or Equal To' },
    { value: LogicType.LessThanOrEqualTo, label: 'Less Than Or Equal To' },
    { value: LogicType.EqualTo, label: 'Equal To' },
    { value: LogicType.True, label: 'True' },
    { value: LogicType.False, label: 'False' },
    { value: LogicType.Updated, label: 'Updated' },
    { value: LogicType.ChangedBy, label: 'Changed By' },
  ];

  readonly comparisonSourceDisabled = computed(() => {
    const logic = this.comparison().logic;
    return logic === LogicType.True || logic === LogicType.False || logic === LogicType.Updated;
  });

  readonly inputChannelUsedIds = computed(() => {
    const comparisonChannelId = this.comparison().channelComparisonId;
    if (!comparisonChannelId) {
      return this.usedChannelIds();
    }

    return [...this.usedChannelIds(), comparisonChannelId];
  });

  readonly comparisonChannelUsedIds = computed(() => {
    const sourceChannelId = this.comparison().channelId;
    if (!sourceChannelId) {
      return this.usedChannelIds();
    }

    return [...this.usedChannelIds(), sourceChannelId];
  });

  onInputChannelChanged(channelId: string | null): void {
    this.updateComparison({ channelId: channelId ?? '' });
  }

  onLogicChanged(value: string): void {
    this.updateComparison({ logic: Number(value) as LogicType });
  }

  onComparisonModeChanged(useStaticComparison: boolean): void {
    if (useStaticComparison) {
      this.updateComparison({
        useStaticComparison: true,
        channelComparisonId: null,
      });
      return;
    }

    this.updateComparison({ useStaticComparison: false });
  }

  onStaticValueChanged(value: string): void {
    this.updateComparison({ staticValueComparison: value });
  }

  onComparisonChannelChanged(channelId: string | null): void {
    this.updateComparison({
      channelComparisonId: channelId,
      useStaticComparison: false,
    });
  }

  onForMsChanged(value: string): void {
    const trimmed = value.trim();
    if (!trimmed) {
      this.updateComparison({ forMs: 0 });
      return;
    }

    const parsed = Number.parseInt(trimmed, 10);
    this.updateComparison({ forMs: Number.isNaN(parsed) ? 0 : Math.max(0, parsed) });
  }

  onReverseResultChanged(checked: boolean): void {
    this.updateComparison({ reverseResult: checked });
  }

  private updateComparison(partial: Partial<ComparisonDefinition>): void {
    this.comparison.set({ ...this.comparison(), ...partial });
  }

  private createEmptyComparison(): ComparisonDefinition {
    return {
      id: '',
      channelId: '',
      logic: LogicType.GreaterThan,
      useStaticComparison: true,
      staticValueComparison: '',
      channelComparisonId: null,
      forMs: 0,
      reverseResult: false,
    };
  }

}

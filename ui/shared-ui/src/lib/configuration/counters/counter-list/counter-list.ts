import { Component, computed, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { CounterDefinition } from '../../../../models/counter-definition';
import { EditCounter } from '../edit-counter/edit-counter';
import { createGuid } from '../../../utils/guid';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

@Component({
  selector: 'lib-counter-list',
  standalone: true,
  imports: [MatIcon, EditCounter],
  templateUrl: './counter-list.html',
  styleUrl: './counter-list.css',
})
export class CounterList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly counterDefinitionsChange = output<CounterDefinition[]>();

  readonly editingCounterId = signal<string | 'new' | null>(null);
  readonly draftCounter = signal<CounterDefinition | null>(null);

  readonly counters = computed<CounterDefinition[]>(() => this.configuration()?.counterDefinitions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);
  readonly outputChannelUsedIds = computed(() => {
    const editingId = this.editingCounterId();
    return this.counters()
      .filter((counter) => (editingId && editingId !== 'new' ? counter.id !== editingId : true))
      .map((counter) => counter.outputChId)
      .filter((channelId) => !!channelId);
  });
  readonly isDraftNameValid = computed(() => {
    const draft = this.draftCounter();
    if (!draft) {
      return false;
    }

    const trimmedLength = draft.name.trim().length;
    return (
      trimmedLength >= 1 &&
      trimmedLength <= 20 &&
      draft.minValue >= -2147483648 &&
      draft.maxValue <= 2147483647
    );
  });
  readonly hasCounters = computed(() => this.counters().length > 0);
  readonly isEditing = computed(() => this.editingCounterId() !== null);

  startAdd(): void {
    this.editingCounterId.set('new');
    this.draftCounter.set(this.createEmptyCounter());
  }

  startEdit(counterId: string): void {
    const existing = this.counters().find((counter) => counter.id === counterId);
    if (!existing) {
      return;
    }

    this.editingCounterId.set(counterId);
    this.draftCounter.set(this.cloneCounter(existing));
  }

  stopEdit(): void {
    this.editingCounterId.set(null);
    this.draftCounter.set(null);
  }

  onDraftCounterChanged(counter: CounterDefinition): void {
    this.draftCounter.set(counter);
  }

  saveDraft(): void {
    const draft = this.draftCounter();
    if (!draft || !this.isDraftNameValid()) {
      return;
    }

    const normalized = this.normalizeCounter({
      ...draft,
      id: draft.id || createGuid(),
      name: draft.name.trim(),
    });

    const existingCounters = this.counters();
    const editingId = this.editingCounterId();

    if (editingId === 'new' || editingId === null) {
      this.counterDefinitionsChange.emit([...existingCounters, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existingCounters.map((counter) =>
      counter.id === editingId ? normalized : counter
    );

    this.counterDefinitionsChange.emit(updated);
    this.stopEdit();
  }

  deleteCounter(counterId: string): void {
    this.counterDefinitionsChange.emit(this.counters().filter((counter) => counter.id !== counterId));

    if (this.editingCounterId() === counterId) {
      this.stopEdit();
    }
  }

  getOutputChannelName(channelId: string): string {
    return this.channels().find((channel) => channel.id === channelId)?.name ?? 'Unknown Channel';
  }

  private normalizeCounter(counter: CounterDefinition): CounterDefinition {
    return {
      ...counter,
      id: counter.id || createGuid(),
      outputChId: counter.outputChId || EMPTY_GUID,
      upChId: counter.upChId || EMPTY_GUID,
      downChId: counter.downChId || EMPTY_GUID,
      resetChId: counter.resetChId || EMPTY_GUID,
    };
  }

  private createEmptyCounter(): CounterDefinition {
    return {
      id: '',
      name: '',
      outputChId: '',
      upChId: '',
      downChId: '',
      resetChId: '',
      maxValue: 2147483647,
      minValue: 0,
      rollAtLimit: false,
      startValue: 0,
      persistValue: false,
    };
  }

  private cloneCounter(counter: CounterDefinition): CounterDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(counter);
    }

    return JSON.parse(JSON.stringify(counter)) as CounterDefinition;
  }

}

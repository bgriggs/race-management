import { Component, computed, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { MathDefinition } from '../../../../models/math-definition';
import { MathType } from '../../../../models/math-type';
import { SimpleOperationType } from '../../../../models/simple-operation-type';
import { EditMath } from '../edit-math/edit-math';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

@Component({
  selector: 'lib-math-list',
  standalone: true,
  imports: [MatIcon, EditMath],
  templateUrl: './math-list.html',
  styleUrl: './math-list.css',
})
export class MathList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly mathDefinitionsChange = output<MathDefinition[]>();

  readonly editingMathId = signal<string | 'new' | null>(null);
  readonly draftMath = signal<MathDefinition | null>(null);

  readonly mathDefinitions = computed<MathDefinition[]>(() => this.configuration()?.mathDefinitions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);
  readonly outputChannelUsedIds = computed(() => {
    const editingId = this.editingMathId();
    return this.mathDefinitions()
      .filter((item) => (editingId && editingId !== 'new' ? item.id !== editingId : true))
      .map((item) => item.outputChannelId)
      .filter((channelId) => !!channelId);
  });
  readonly isDraftNameValid = computed(() => {
    const draft = this.draftMath();
    if (!draft) {
      return false;
    }

    const trimmedLength = draft.name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly hasMathDefinitions = computed(() => this.mathDefinitions().length > 0);
  readonly isEditing = computed(() => this.editingMathId() !== null);

  startAdd(): void {
    this.editingMathId.set('new');
    this.draftMath.set(this.createEmptyMath());
  }

  startEdit(mathId: string): void {
    const existing = this.mathDefinitions().find((item) => item.id === mathId);
    if (!existing) {
      return;
    }

    this.editingMathId.set(mathId);
    this.draftMath.set(this.cloneMath(existing));
  }

  stopEdit(): void {
    this.editingMathId.set(null);
    this.draftMath.set(null);
  }

  onDraftMathChanged(math: MathDefinition): void {
    this.draftMath.set(math);
  }

  saveDraft(): void {
    const draft = this.draftMath();
    if (!draft || !this.isDraftNameValid()) {
      return;
    }

    const normalized = this.normalizeMath({
      ...draft,
      id: draft.id || this.createGuid(),
      name: draft.name.trim(),
    });

    const existing = this.mathDefinitions();
    const editingId = this.editingMathId();

    if (editingId === 'new' || editingId === null) {
      this.mathDefinitionsChange.emit([...existing, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existing.map((item) => (item.id === editingId ? normalized : item));
    this.mathDefinitionsChange.emit(updated);
    this.stopEdit();
  }

  deleteMath(mathId: string): void {
    this.mathDefinitionsChange.emit(this.mathDefinitions().filter((item) => item.id !== mathId));

    if (this.editingMathId() === mathId) {
      this.stopEdit();
    }
  }

  getOutputChannelName(channelId: string): string {
    return this.channels().find((channel) => channel.id === channelId)?.name ?? 'Unknown Channel';
  }

  private normalizeMath(math: MathDefinition): MathDefinition {
    return {
      ...math,
      id: math.id || this.createGuid(),
      channel1Id: math.channel1Id || EMPTY_GUID,
      channel2Id: math.channel2Id || EMPTY_GUID,
      outputChannelId: math.outputChannelId || EMPTY_GUID,
    };
  }

  private createEmptyMath(): MathDefinition {
    return {
      id: '',
      name: '',
      type: MathType.Bias,
      a: 0,
      b: 0,
      channel1Id: '',
      channel2Id: null,
      outputChannelId: '',
      simpleOperationType: SimpleOperationType.Add,
    };
  }

  private cloneMath(math: MathDefinition): MathDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(math);
    }

    return JSON.parse(JSON.stringify(math)) as MathDefinition;
  }

  private createGuid(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = Math.floor(Math.random() * 16);
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}

import { Component, computed, input, output, signal } from '@angular/core';
import { createGuid } from '../../../utils/guid';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { EnumDefinition } from '../../../../models/enum-definition';
import { EnumValueDefinition } from '../../../../models/enum-value-definition';
import { EditEnum } from '../edit-enum/edit-enum';

@Component({
  selector: 'lib-enum-list',
  standalone: true,
  imports: [MatIcon, EditEnum],
  templateUrl: './enum-list.html',
  styleUrl: './enum-list.css',
})
export class EnumList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly enumDefinitionsChange = output<EnumDefinition[]>();

  readonly editingEnumId = signal<string | 'new' | null>(null);
  readonly draftEnum = signal<EnumDefinition | null>(null);

  readonly enums = computed<EnumDefinition[]>(() => this.configuration()?.enumDefinitions ?? []);
  readonly isDraftNameValid = computed(() => {
    const draft = this.draftEnum();
    if (!draft) {
      return false;
    }

    const trimmedLength = draft.name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 20;
  });
  readonly isDraftValuesValid = computed(() => {
    const draft = this.draftEnum();
    if (!draft) {
      return false;
    }

    return draft.values.every((row) =>
      Number.isInteger(row.value) && row.source.trim().length >= 1 && row.source.trim().length <= 10
    );
  });
  readonly isDraftValid = computed(() => this.isDraftNameValid() && this.isDraftValuesValid());
  readonly hasEnums = computed(() => this.enums().length > 0);
  readonly isEditing = computed(() => this.editingEnumId() !== null);

  startAdd(): void {
    this.editingEnumId.set('new');
    this.draftEnum.set(this.createEmptyEnum());
  }

  startEdit(enumId: string): void {
    const existing = this.enums().find((item) => item.id === enumId);
    if (!existing) {
      return;
    }

    this.editingEnumId.set(enumId);
    this.draftEnum.set(this.sortEnumValues(this.cloneEnum(existing)));
  }

  stopEdit(): void {
    this.editingEnumId.set(null);
    this.draftEnum.set(null);
  }

  onDraftEnumChanged(enumDef: EnumDefinition): void {
    this.draftEnum.set(enumDef);
  }

  saveDraft(): void {
    const draft = this.draftEnum();
    if (!draft || !this.isDraftValid()) {
      return;
    }

    const normalized: EnumDefinition = {
      ...this.sortEnumValues(draft),
      id: draft.id || createGuid(),
      name: draft.name.trim(),
    };

    const existing = this.enums();
    const editingId = this.editingEnumId();

    if (editingId === 'new' || editingId === null) {
      this.enumDefinitionsChange.emit([...existing, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existing.map((item) => (item.id === editingId ? normalized : item));
    this.enumDefinitionsChange.emit(updated);
    this.stopEdit();
  }

  deleteEnum(enumId: string): void {
    this.enumDefinitionsChange.emit(this.enums().filter((item) => item.id !== enumId));

    if (this.editingEnumId() === enumId) {
      this.stopEdit();
    }
  }

  getValuesSummary(values: EnumValueDefinition[]): string {
    return values
      .slice(0, 3)
      .map((v) => `(${v.value}) ${v.source}`)
      .join(', ');
  }

  private createEmptyEnum(): EnumDefinition {
    const def = new EnumDefinition();
    def.id = '';
    def.name = '';
    def.values = [];
    return def;
  }

  private cloneEnum(enumDef: EnumDefinition): EnumDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(enumDef);
    }

    return JSON.parse(JSON.stringify(enumDef)) as EnumDefinition;
  }

  private sortEnumValues(enumDef: EnumDefinition): EnumDefinition {
    return {
      ...enumDef,
      values: [...enumDef.values].sort((a, b) => a.value - b.value),
    };
  }

}

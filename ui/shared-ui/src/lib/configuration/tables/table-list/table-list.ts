import { Component, computed, input, output, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { CarConfiguration } from '../../../../models/car-configuration';
import { TableDefinition } from '../../../../models/table-definition';
import { EditTable } from '../edit-table/edit-table';
import { createGuid } from '../../../utils/guid';

@Component({
  selector: 'lib-table-list',
  imports: [MatIcon, EditTable],
  templateUrl: './table-list.html',
  styleUrl: './table-list.css',
})
export class TableList {
  readonly configuration = input<CarConfiguration | null>(null);
  readonly tableDefinitionsChange = output<TableDefinition[]>();

  readonly editingTableId = signal<string | 'new' | null>(null);
  readonly draftTable = signal<TableDefinition | null>(null);

  readonly tables = computed<TableDefinition[]>(() => this.configuration()?.tableDefinitions ?? []);
  readonly channels = computed(() => this.configuration()?.channelDefinitions ?? []);
  readonly isDraftNameValid = computed(() => {
    const draft = this.draftTable();
    if (!draft) {
      return false;
    }
    const trimmedLength = draft.name.trim().length;
    return trimmedLength >= 1 && trimmedLength <= 50;
  });
  readonly hasTables = computed(() => this.tables().length > 0);
  readonly isEditing = computed(() => this.editingTableId() !== null);

  startAdd(): void {
    this.editingTableId.set('new');
    this.draftTable.set(this.createEmptyTable());
  }

  startEdit(tableId: string): void {
    const existing = this.tables().find((t) => t.id === tableId);
    if (!existing) {
      return;
    }
    this.editingTableId.set(tableId);
    this.draftTable.set(this.cloneTable(existing));
  }

  stopEdit(): void {
    this.editingTableId.set(null);
    this.draftTable.set(null);
  }

  onDraftTableChanged(table: TableDefinition): void {
    this.draftTable.set(table);
  }

  saveDraft(): void {
    const draft = this.draftTable();
    if (!draft || !this.isDraftNameValid()) {
      return;
    }

    const normalized: TableDefinition = {
      ...draft,
      id: draft.id || createGuid(),
      name: draft.name.trim(),
    };

    const existingTables = this.tables();
    const editingId = this.editingTableId();

    if (editingId === 'new' || editingId === null) {
      this.tableDefinitionsChange.emit([...existingTables, normalized]);
      this.stopEdit();
      return;
    }

    const updated = existingTables.map((t) => (t.id === editingId ? normalized : t));
    this.tableDefinitionsChange.emit(updated);
    this.stopEdit();
  }

  deleteTable(tableId: string): void {
    this.tableDefinitionsChange.emit(this.tables().filter((t) => t.id !== tableId));
    if (this.editingTableId() === tableId) {
      this.stopEdit();
    }
  }

  getChannelName(channelId: string): string {
    return this.channels().find((ch) => ch.id === channelId)?.name ?? 'Unknown Channel';
  }

  getEntryCount(table: TableDefinition): number {
    return table.mappings.length;
  }

  private createEmptyTable(): TableDefinition {
    return {
      id: '',
      name: '',
      isEnum: false,
      ignoreCase: false,
      inputChannel: '',
      outputChannel: '',
      interpolationType: 0,
      mappings: [],
    };
  }

  private cloneTable(table: TableDefinition): TableDefinition {
    if (typeof structuredClone === 'function') {
      return structuredClone(table);
    }
    return JSON.parse(JSON.stringify(table)) as TableDefinition;
  }
}

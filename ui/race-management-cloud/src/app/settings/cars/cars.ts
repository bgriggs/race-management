import { Component, computed, effect, inject, signal } from '@angular/core';
import { Car } from '../../../../../shared-ui/src/cloud-api/car';
import { CarRequest } from '../../../../../shared-ui/src/cloud-api/car-request';
import { CarUpdate } from '../../../../../shared-ui/src/cloud-api/car-update';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';

type EditingState = { mode: 'new' } | { mode: 'edit'; carNumber: string } | null;

@Component({
  selector: 'app-cars',
  imports: [],
  templateUrl: './cars.html',
  styleUrl: './cars.css',
})
export class Cars {
  private readonly client = inject(ConfigurationClient);
  protected readonly teamSelection = inject(TeamSelectionService);

  protected readonly cars = signal<Car[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<EditingState>(null);
  protected readonly draftNumber = signal('');
  protected readonly draftMake = signal('');
  protected readonly draftModel = signal('');
  protected readonly draftColor = signal('');
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly isEditing = computed(() => this.editing() !== null);
  protected readonly isEditMode = computed(() => this.editing()?.mode === 'edit');
  protected readonly editorTitle = computed(() => this.isEditMode() ? 'Edit Car' : 'Add Car');
  protected readonly isDraftValid = computed(() =>
    this.draftNumber().trim().length > 0 &&
    this.draftMake().trim().length > 0 &&
    this.draftModel().trim().length > 0 &&
    this.draftColor().trim().length > 0,
  );

  constructor() {
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this.cars.set([]);
        return;
      }
      void this.load(teamId);
    });
  }

  protected startAdd(): void {
    this.draftNumber.set('');
    this.draftMake.set('');
    this.draftModel.set('');
    this.draftColor.set('');
    this.saveError.set(null);
    this.editing.set({ mode: 'new' });
  }

  protected startEdit(car: Car): void {
    this.draftNumber.set(car.number);
    this.draftMake.set(car.make);
    this.draftModel.set(car.model);
    this.draftColor.set(car.color);
    this.saveError.set(null);
    this.editing.set({ mode: 'edit', carNumber: car.number });
  }

  protected stopEdit(): void {
    if (this.saving()) return;
    this.editing.set(null);
    this.saveError.set(null);
  }

  protected onNumberInput(value: string): void { this.draftNumber.set(value); }
  protected onMakeInput(value: string): void { this.draftMake.set(value); }
  protected onModelInput(value: string): void { this.draftModel.set(value); }
  protected onColorInput(value: string): void { this.draftColor.set(value); }

  protected async saveDraft(): Promise<void> {
    const state = this.editing();
    const teamId = this.teamSelection.selectedTeamId();
    if (!state || teamId === null || !this.isDraftValid() || this.saving()) return;

    this.saving.set(true);
    this.saveError.set(null);
    try {
      if (state.mode === 'new') {
        const request: CarRequest = {
          number: this.draftNumber().trim(),
          make: this.draftMake().trim(),
          model: this.draftModel().trim(),
          color: this.draftColor().trim(),
        };
        await this.client.createCar(teamId, request);
      } else {
        const update: CarUpdate = {
          make: this.draftMake().trim(),
          model: this.draftModel().trim(),
          color: this.draftColor().trim(),
        };
        await this.client.updateCar(teamId, state.carNumber, update);
      }
      this.editing.set(null);
      await this.load(teamId);
    } catch (err) {
      console.error('Failed to save car:', err);
      this.saveError.set('Failed to save car. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteCar(car: Car): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) return;
    if (!confirm(`Delete car "${car.number}"? This cannot be undone.`)) return;

    this.error.set(null);
    try {
      await this.client.deleteCar(teamId, car.number);
      await this.load(teamId);
    } catch (err) {
      console.error('Failed to delete car:', err);
      this.error.set('Failed to delete car.');
    }
  }

  private async load(teamId: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.cars.set(await this.client.listCars(teamId));
    } catch (err) {
      console.error('Failed to load cars:', err);
      this.error.set('Failed to load cars.');
      this.cars.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}

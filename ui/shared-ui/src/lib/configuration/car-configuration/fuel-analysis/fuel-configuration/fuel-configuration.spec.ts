import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

import { FuelConfiguration } from './fuel-configuration';
import { CarFuelConfig } from '../../../../../models/car-fuel-config';

function makeConfig(overrides: Partial<CarFuelConfig> = {}): CarFuelConfig {
  return {
    isEnabled: true,
    tankCapacityGallons: 15,
    defaultConsumptionGalPerMin: 0.3,
    defaultYellowConsumptionMultiplier: 0.5,
    defaultCode35ConsumptionMultiplier: 0.3,
    fuelLevelChannelId: 'a2529acf-a7c6-449f-8a85-c7d76b35dbcb',
    tripFuelChannelId: 'acd3d127-acaf-4f8a-b27a-8623cfda09f3',
    fuelUsedChannelId: '740ce2a6-dc88-4425-85dc-7f99f2a902f1',
    fuelFullChannelId: 'c3b94831-95f6-4935-bf67-1aacfd611f75',
    inPitChannelId: 'da12563a-1167-4899-9956-700b0b693005',
    throttleConsumption: {
      isEnabled: false,
      maxRpm: 7000,
      throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
      engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
    },
    ...overrides,
  };
}

describe('FuelConfiguration', () => {
  let component: FuelConfiguration;
  let fixture: ComponentFixture<FuelConfiguration>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FuelConfiguration, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(FuelConfiguration);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('form is valid even when all fuel-signal channel fields are empty (they are optional)', async () => {
    fixture.componentRef.setInput('configuration', makeConfig());
    fixture.detectChanges();
    await fixture.whenStable();

    component.form.controls.fuelLevelChannelId.setValue('');
    component.form.controls.tripFuelChannelId.setValue('');
    component.form.controls.fuelUsedChannelId.setValue('');
    component.form.controls.fuelFullChannelId.setValue('');

    expect(component.form.controls.fuelLevelChannelId.valid).toBe(true);
    expect(component.form.controls.tripFuelChannelId.valid).toBe(true);
    expect(component.form.controls.fuelUsedChannelId.valid).toBe(true);
    expect(component.form.controls.fuelFullChannelId.valid).toBe(true);
  });

  it('emits CarFuelConfig with the new fuelLevelChannelId when picker changes', async () => {
    fixture.componentRef.setInput('configuration', makeConfig());
    fixture.detectChanges();
    await fixture.whenStable();

    const customId = '11111111-1111-1111-1111-111111111111';
    let emitted: CarFuelConfig | null = null;
    component.configurationChange.subscribe((c: CarFuelConfig) => { emitted = c; });

    component.onChannelChange('fuelLevelChannelId', customId);

    expect(emitted).not.toBeNull();
    expect(emitted!.fuelLevelChannelId).toBe(customId);
    // Other bindings preserved from input config.
    expect(emitted!.tripFuelChannelId).toBe('acd3d127-acaf-4f8a-b27a-8623cfda09f3');
    // throttleConsumption nested payload preserved (channel IDs + maxRpm).
    expect(emitted!.throttleConsumption.maxRpm).toBe(7000);
    expect(emitted!.throttleConsumption.throttlePositionChannelId).toBe('c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01');
  });

  it('clearing InPit channel emits null (binding becomes disabled)', async () => {
    fixture.componentRef.setInput('configuration', makeConfig());
    fixture.detectChanges();
    await fixture.whenStable();

    let emitted: CarFuelConfig | null = null;
    component.configurationChange.subscribe((c: CarFuelConfig) => { emitted = c; });

    component.onInPitChannelChange(null);

    expect(emitted).not.toBeNull();
    expect(emitted!.inPitChannelId).toBeNull();
  });
});

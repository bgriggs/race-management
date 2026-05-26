import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThrottleConsumptionConfig } from './throttle-consumption-config';
import { ThrottleConsumptionConfig as ThrottleConsumptionConfigModel } from '../../../../../models/throttle-consumption-config';

function makeConfig(overrides: Partial<ThrottleConsumptionConfigModel> = {}): ThrottleConsumptionConfigModel {
  return {
    isEnabled: true,
    maxRpm: 7000,
    throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
    engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
    ...overrides,
  };
}

describe('ThrottleConsumptionConfig', () => {
  let component: ThrottleConsumptionConfig;
  let fixture: ComponentFixture<ThrottleConsumptionConfig>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ThrottleConsumptionConfig],
    }).compileComponents();

    fixture = TestBed.createComponent(ThrottleConsumptionConfig);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('marks ThrottlePosition and EngineRpm channel fields as required', async () => {
    fixture.componentRef.setInput('configuration', makeConfig());
    fixture.detectChanges();
    await fixture.whenStable();

    component.form.controls.throttlePositionChannelId.setValue('');
    component.form.controls.engineRpmChannelId.setValue('');

    expect(component.form.controls.throttlePositionChannelId.valid).toBe(false);
    expect(component.form.controls.engineRpmChannelId.valid).toBe(false);
  });

  it('emits ThrottleConsumptionConfig with updated channel ID when picker changes', async () => {
    fixture.componentRef.setInput('configuration', makeConfig());
    fixture.detectChanges();
    await fixture.whenStable();

    const customId = '99999999-9999-9999-9999-999999999999';
    let emitted: ThrottleConsumptionConfigModel | null = null;
    component.configurationChange.subscribe((c: ThrottleConsumptionConfigModel) => { emitted = c; });

    component.onChannelChange('throttlePositionChannelId', customId);

    expect(emitted).not.toBeNull();
    expect(emitted!.throttlePositionChannelId).toBe(customId);
    // EngineRpm + scalar settings preserved.
    expect(emitted!.engineRpmChannelId).toBe('74c57a58-d78d-499a-977b-11cee221926a');
    expect(emitted!.maxRpm).toBe(7000);
    expect(emitted!.isEnabled).toBe(true);
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditChannel } from './edit-channel';
import { MANAGEMENT_DATA_CLIENT, type ManagementDataClient } from '../../../data/management-data-client';
import { ChannelDefinition } from '../../../../models/channel-definition';
import { ChannelDistribution } from '../../../../models/channel-distribution';
import { ChannelScope } from '../../../../models/channel-scope';

function makeChannel(overrides: Partial<ChannelDefinition> = {}): ChannelDefinition {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    isReserved: false,
    category: '',
    name: 'TestCh',
    abbreviation: 'TCH',
    dataType: 'Temperature',
    baseUnitType: 'DegreeFahrenheit',
    outputUnitType: 'DegreeFahrenheit',
    outputDecimalPlaces: 1,
    lowRange: 0,
    highRange: 100,
    defaultValue: 0,
    groupTag: '',
    enumConversion: null,
    timeoutMs: 0,
    distribution: ChannelDistribution.CarToCloud,
    isDistributionLocked: false,
    scope: ChannelScope.PerCar,
    managedByFeature: null,
    ...overrides
  };
}

describe('EditChannel', () => {
  let component: EditChannel;
  let fixture: ComponentFixture<EditChannel>;
  let mockClient: ManagementDataClient;

  beforeEach(async () => {
    mockClient = {
      listDiscoveredRacecarsAsync: vi.fn().mockResolvedValue([]),
      getActiveRacecarAsync: vi.fn().mockResolvedValue(null),
      selectRacecarAsync: vi.fn(),
      loadCarConfigurationSummariesAsync: vi.fn(),
      loadReservedChannelDefinitionsAsync: vi.fn().mockResolvedValue([]),
      loadAvailableUnitTypesAsync: vi.fn().mockResolvedValue([]),
      loadCarConfigurationAsync: vi.fn(),
      saveCarConfigurationAsync: vi.fn(),
      transmitToCarAsync: vi.fn(),
      deleteCarConfigurationAsync: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [EditChannel],
      providers: [{ provide: MANAGEMENT_DATA_CLIENT, useValue: mockClient }]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditChannel);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('defaults to reserved kind for a new channel', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.kind()).toBe('reserved');
    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector('input#channel-name')).toBeFalsy();
  });

  it('switches the Name field to a textbox when Custom radio is clicked', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const root: HTMLElement = fixture.nativeElement;
    const customRadio = root.querySelector<HTMLInputElement>('input[type="radio"][value="custom"]')!;
    customRadio.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.kind()).toBe('custom');
    expect(root.querySelector('input#channel-name')).toBeTruthy();
    expect(root.querySelector('select#channel-name')).toBeFalsy();
  });

  it('does not reset kind on subsequent change detection cycles', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    expect(component.kind()).toBe('reserved');

    component.kind.set('custom');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.kind()).toBe('custom');
  });

  it('shows Origin radios for a new custom channel and hides them when editing existing', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const root: HTMLElement = fixture.nativeElement;
    const customRadio = root.querySelector<HTMLInputElement>('input[type="radio"][value="custom"]')!;
    customRadio.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    // Origin radios visible for new-custom flow.
    expect(root.querySelector('input[type="radio"][value="Car"]')).toBeTruthy();
    expect(root.querySelector('input[type="radio"][value="Cloud"]')).toBeTruthy();

    // Now bind an existing channel — radios should hide.
    fixture.componentRef.setInput('channel', makeChannel({ isReserved: false }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(root.querySelector('input[type="radio"][value="Car"]')).toBeFalsy();
    expect(root.querySelector('input[type="radio"][value="Cloud"]')).toBeFalsy();
  });

  it('picking Cloud origin defaults distribution to CloudLocal with CloudToCar as the other option', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const root: HTMLElement = fixture.nativeElement;
    root.querySelector<HTMLInputElement>('input[type="radio"][value="custom"]')!
      .dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    component.onOriginChange('Cloud');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.origin()).toBe('Cloud');
    expect(component.form.controls.distribution.value).toBe(ChannelDistribution.CloudLocal);
    const options = component.distributionOptions().map(o => o.value);
    expect(options).toEqual([ChannelDistribution.CloudToCar, ChannelDistribution.CloudLocal]);
  });

  it('disables the distribution dropdown when isDistributionLocked is true on the channel', async () => {
    const locked = makeChannel({
      isReserved: true,
      distribution: ChannelDistribution.CarToCloud,
      isDistributionLocked: true,
      managedByFeature: 'throttle-consumption'
    });
    fixture.componentRef.setInput('channel', locked);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isDistributionLocked()).toBe(true);
    expect(component.form.controls.distribution.disabled).toBe(true);
  });

  it('enables the distribution dropdown with origin-matching options on a non-locked reserved channel', async () => {
    const editable = makeChannel({
      isReserved: true,
      distribution: ChannelDistribution.CloudLocal,
      isDistributionLocked: false,
      managedByFeature: 'fuel-analysis'
    });
    fixture.componentRef.setInput('channel', editable);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.form.controls.distribution.disabled).toBe(false);
    expect(component.origin()).toBe('Cloud');
    const options = component.distributionOptions().map(o => o.value);
    expect(options).toEqual([ChannelDistribution.CloudToCar, ChannelDistribution.CloudLocal]);
  });

  it('preserves isDistributionLocked from the existing reserved channel when saving', async () => {
    const locked = makeChannel({
      isReserved: true,
      distribution: ChannelDistribution.CarToCloud,
      isDistributionLocked: true,
      managedByFeature: 'throttle-consumption'
    });
    fixture.componentRef.setInput('channel', locked);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    let emitted: ChannelDefinition | null = null;
    component.save.subscribe((c: ChannelDefinition) => { emitted = c; });
    component.submit();

    expect(emitted).not.toBeNull();
    expect(emitted!.isDistributionLocked).toBe(true);
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditChannel } from './edit-channel';
import { MANAGEMENT_DATA_CLIENT, type ManagementDataClient } from '../../../data/management-data-client';

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
});

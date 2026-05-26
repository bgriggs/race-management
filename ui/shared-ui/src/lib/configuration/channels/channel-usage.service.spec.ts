import { ChannelUsageService } from './channel-usage.service';
import { CanBusInterfaceConfig } from '../../../models/can-bus-interface-config';
import { ConditionDefinition } from '../../../models/condition-definition';
import { MathDefinition } from '../../../models/math-definition';
import { MathType } from '../../../models/math-type';
import { SimpleOperationType } from '../../../models/simple-operation-type';
import { CounterDefinition } from '../../../models/counter-definition';
import { TimerDefinition } from '../../../models/timer-definition';
import { TableDefinition } from '../../../models/table-definition';
import { InterpolationType } from '../../../models/interpolation-type';
import { AlarmDefinition } from '../../../models/alarm-definition';
import { StatementDefinition } from '../../../models/statement-definition';

const emptyStatement = (): StatementDefinition => ({
  id: '',
  activateComparisons: [],
  deactivateComparisons: null,
});

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const mkCounter = (overrides: Partial<CounterDefinition>): CounterDefinition => ({
  id: '',
  name: '',
  outputChId: EMPTY_GUID,
  upChId: EMPTY_GUID,
  downChId: EMPTY_GUID,
  resetChId: EMPTY_GUID,
  maxValue: 0,
  minValue: 0,
  rollAtLimit: false,
  startValue: 0,
  persistValue: false,
  ...overrides,
});

const mkTimer = (overrides: Partial<TimerDefinition>): TimerDefinition => ({
  id: '',
  name: '',
  outputChId: EMPTY_GUID,
  statement: emptyStatement(),
  countDown: false,
  enableRollover: false,
  rolloverSeconds: 0,
  enableStartSeconds: false,
  startSeconds: 0,
  enableStopSeconds: false,
  stopSeconds: 0,
  ...overrides,
});

const mkTable = (overrides: Partial<TableDefinition>): TableDefinition => ({
  id: '',
  name: '',
  isEnum: false,
  ignoreCase: false,
  inputChannel: EMPTY_GUID,
  outputChannel: EMPTY_GUID,
  interpolationType: InterpolationType.Linear,
  mappings: [],
  ...overrides,
});

const mkAlarm = (overrides: Partial<AlarmDefinition>): AlarmDefinition => ({
  id: '',
  name: '',
  statement: emptyStatement(),
  messsage: '',
  displayChannelSourceColorHex: '',
  timeAfterAckToDisplaySecs: 0,
  alarmStatusChannelId: null,
  ...overrides,
});

describe('ChannelUsageService', () => {
  let service: ChannelUsageService;

  beforeEach(() => {
    service = new ChannelUsageService();
  });

  it('tracks used channels from receive CAN mappings', () => {
    const canInterfaces: CanBusInterfaceConfig[] = [
      {
        interfaceName: 'can0',
        bitRate: 500000,
        silentOnCanBus: false,
        messages: [
          {
            isEnabled: true,
            canId: 0x123,
            isExtended: false,
            length: 8,
            isBigEndian: true,
            isReceive: true,
            transmitRate: '00:00:01',
            channelAssignments: [
              {
                id: 'channel-a',
                offset: 0,
                length: 1,
                mask: 0xFF,
                isSigned: false,
                formulaMultiplier: 1,
                formulaDivider: 1,
                formulaConst: 0,
              },
              {
                id: 'channel-b',
                offset: 1,
                length: 1,
                mask: 0xFF,
                isSigned: false,
                formulaMultiplier: 1,
                formulaDivider: 1,
                formulaConst: 0,
              },
            ],
          },
        ],
      },
    ];

    const usedChannelIds = service.getUsedChannelIdsFromCanInterfaces(canInterfaces);

    expect(usedChannelIds).toEqual(['channel-a', 'channel-b']);
  });

  it('does not track channels from transmit CAN mappings', () => {
    const canInterfaces: CanBusInterfaceConfig[] = [
      {
        interfaceName: 'can0',
        bitRate: 500000,
        silentOnCanBus: false,
        messages: [
          {
            isEnabled: true,
            canId: 0x123,
            isExtended: false,
            length: 8,
            isBigEndian: true,
            isReceive: false,
            transmitRate: '00:00:01',
            channelAssignments: [
              {
                id: 'channel-a',
                offset: 0,
                length: 1,
                mask: 0xFF,
                isSigned: false,
                formulaMultiplier: 1,
                formulaDivider: 1,
                formulaConst: 0,
              },
            ],
          },
        ],
      },
    ];

    const usedChannelIds = service.getUsedChannelIdsFromCanInterfaces(canInterfaces);

    expect(usedChannelIds).toEqual([]);
  });

  it('de-duplicates channel ids across interfaces and messages', () => {
    const canInterfaces: CanBusInterfaceConfig[] = [
      {
        interfaceName: 'can0',
        bitRate: 500000,
        silentOnCanBus: false,
        messages: [
          {
            isEnabled: true,
            canId: 0x123,
            isExtended: false,
            length: 8,
            isBigEndian: true,
            isReceive: true,
            transmitRate: '00:00:01',
            channelAssignments: [
              {
                id: 'channel-a',
                offset: 0,
                length: 1,
                mask: 0xFF,
                isSigned: false,
                formulaMultiplier: 1,
                formulaDivider: 1,
                formulaConst: 0,
              },
            ],
          },
        ],
      },
      {
        interfaceName: 'can1',
        bitRate: 500000,
        silentOnCanBus: false,
        messages: [
          {
            isEnabled: true,
            canId: 0x456,
            isExtended: false,
            length: 8,
            isBigEndian: true,
            isReceive: true,
            transmitRate: '00:00:01',
            channelAssignments: [
              {
                id: 'channel-a',
                offset: 0,
                length: 1,
                mask: 0xFF,
                isSigned: false,
                formulaMultiplier: 1,
                formulaDivider: 1,
                formulaConst: 0,
              },
            ],
          },
        ],
      },
    ];

    const usedChannelIds = service.getUsedChannelIdsFromCanInterfaces(canInterfaces);

    expect(usedChannelIds).toEqual(['channel-a']);
  });

  it('tracks used channels from user condition output channels', () => {
    const userConditions: ConditionDefinition[] = [
      {
        id: 'condition-1',
        name: 'Condition A',
        outputChannelId: 'channel-a',
        statements: [],
      },
      {
        id: 'condition-2',
        name: 'Condition B',
        outputChannelId: 'channel-b',
        statements: [],
      },
      {
        id: 'condition-3',
        name: 'Condition C',
        outputChannelId: 'channel-a',
        statements: [],
      },
      {
        id: 'condition-4',
        name: 'Condition D',
        outputChannelId: '00000000-0000-0000-0000-000000000000',
        statements: [],
      },
    ];

    const usedChannelIds = service.getUsedChannelIdsFromUserConditions(userConditions);

    expect(usedChannelIds).toEqual(['channel-a', 'channel-b']);
  });

  it('combines CAN mappings and user condition outputs when reading from full config inputs', () => {
    const canInterfaces: CanBusInterfaceConfig[] = [
      {
        interfaceName: 'can0',
        bitRate: 500000,
        silentOnCanBus: false,
        messages: [
          {
            isEnabled: true,
            canId: 0x123,
            isExtended: false,
            length: 8,
            isBigEndian: true,
            isReceive: true,
            transmitRate: '00:00:01',
            channelAssignments: [
              {
                id: 'channel-a',
                offset: 0,
                length: 1,
                mask: 0xFF,
                isSigned: false,
                formulaMultiplier: 1,
                formulaDivider: 1,
                formulaConst: 0,
              },
            ],
          },
        ],
      },
    ];

    const userConditions: ConditionDefinition[] = [
      {
        id: 'condition-1',
        name: 'Condition A',
        outputChannelId: 'channel-b',
        statements: [],
      },
      {
        id: 'condition-2',
        name: 'Condition B',
        outputChannelId: 'channel-a',
        statements: [],
      },
    ];

    const usedChannelIds = service.getUsedChannelIdsFromCanConfig(
      { canBusEnabled: [true], interfaces: canInterfaces },
      userConditions
    );

    expect(usedChannelIds).toEqual(['channel-a', 'channel-b']);
  });
});

describe('ChannelUsageService - per-type output methods', () => {
  let service: ChannelUsageService;

  beforeEach(() => {
    service = new ChannelUsageService();
  });

  it('tracks math definition output channels', () => {
    const defs: MathDefinition[] = [
      { id: 'm1', name: 'M1', type: MathType.SimpleOperation, a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: 'ch-out-a', simpleOperationType: SimpleOperationType.Add },
      { id: 'm2', name: 'M2', type: MathType.SimpleOperation, a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: 'ch-out-b', simpleOperationType: SimpleOperationType.Add },
      { id: 'm3', name: 'M3', type: MathType.SimpleOperation, a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: 'ch-out-a', simpleOperationType: SimpleOperationType.Add },
      { id: 'm4', name: 'M4', type: MathType.SimpleOperation, a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: '00000000-0000-0000-0000-000000000000', simpleOperationType: SimpleOperationType.Add },
    ];

    expect(service.getUsedChannelIdsFromMathDefinitions(defs)).toEqual(['ch-out-a', 'ch-out-b']);
  });

  it('tracks counter definition output channels, ignores empty guid', () => {
    const defs: CounterDefinition[] = [
      mkCounter({ id: 'c1', outputChId: 'ch-counter-a', upChId: 'ch-up' }),
      mkCounter({ id: 'c2', outputChId: EMPTY_GUID, upChId: 'ch-up' }),
    ];

    expect(service.getUsedChannelIdsFromCounterDefinitions(defs)).toEqual(['ch-counter-a']);
  });

  it('tracks timer definition output channels, ignores empty guid', () => {
    const defs: TimerDefinition[] = [
      mkTimer({ id: 't1', outputChId: 'ch-timer-a' }),
      mkTimer({ id: 't2', outputChId: EMPTY_GUID }),
    ];

    expect(service.getUsedChannelIdsFromTimerDefinitions(defs)).toEqual(['ch-timer-a']);
  });

  it('tracks table definition output channels, ignores empty guid', () => {
    const defs: TableDefinition[] = [
      mkTable({ id: 'tbl1', outputChannel: 'ch-table-a', inputChannel: 'ch-in' }),
      mkTable({ id: 'tbl2', outputChannel: EMPTY_GUID, inputChannel: 'ch-in' }),
    ];

    expect(service.getUsedChannelIdsFromTableDefinitions(defs)).toEqual(['ch-table-a']);
  });

  it('tracks alarm definition status output channels, ignores null and empty guid', () => {
    const defs: AlarmDefinition[] = [
      mkAlarm({ id: 'a1', alarmStatusChannelId: 'ch-alarm-a' }),
      mkAlarm({ id: 'a2', alarmStatusChannelId: null }),
      mkAlarm({ id: 'a3', alarmStatusChannelId: EMPTY_GUID }),
    ];

    expect(service.getUsedChannelIdsFromAlarmDefinitions(defs)).toEqual(['ch-alarm-a']);
  });

  it('updateFromConfiguration sets usedChannelIds from all output sources', () => {
    service.updateFromConfiguration({
      configurationId: 'cfg-1',
      configurationSchemaVersion: 1,
      name: 'Test',
      notes: '',
      lastUpdated: new Date(),
      lastUpdatedOnCarTimestamp: null,
      car: '77',
      isCloudConnectionEnabled: false,
      clientId: '',
      clientSecret: '',
      canConfig: {
        canBusEnabled: [true],
        interfaces: [
          {
            interfaceName: 'can0',
            bitRate: 500000,
            silentOnCanBus: false,
            messages: [
              {
                isEnabled: true,
                canId: 0x100,
                isExtended: false,
                length: 8,
                isBigEndian: true,
                isReceive: true,
                transmitRate: '00:00:01',
                channelAssignments: [{ id: 'ch-can', offset: 0, length: 1, mask: 0xFF, isSigned: false, formulaMultiplier: 1, formulaDivider: 1, formulaConst: 0 }],
              },
            ],
          },
        ],
      },
      channelDefinitions: [],
      alarmDefinitions: [{ id: 'a1', name: 'A', statement: emptyStatement(), messsage: '', displayChannelSourceColorHex: '', timeAfterAckToDisplaySecs: 0, alarmStatusChannelId: 'ch-alarm' }],
      counterDefinitions: [{ id: 'c1', name: 'C', outputChId: 'ch-counter', upChId: '00000000-0000-0000-0000-000000000000', downChId: '00000000-0000-0000-0000-000000000000', resetChId: '00000000-0000-0000-0000-000000000000', maxValue: 0, minValue: 0, rollAtLimit: false, startValue: 0, persistValue: false }],
      mathDefinitions: [{ id: 'm1', name: 'M', type: MathType.SimpleOperation, a: 0, b: 0, channel1Id: 'ch-can', channel2Id: null, outputChannelId: 'ch-math', simpleOperationType: SimpleOperationType.Add }],
      tableDefinitions: [{ id: 'tbl1', name: 'T', isEnum: false, ignoreCase: false, inputChannel: 'ch-can', outputChannel: 'ch-table', interpolationType: InterpolationType.Linear, mappings: [] }],
      timerDefinitions: [{ id: 't1', name: 'T', outputChId: 'ch-timer', statement: emptyStatement(), countDown: false, enableRollover: false, rolloverSeconds: 0, enableStartSeconds: false, startSeconds: 0, enableStopSeconds: false, stopSeconds: 0 }],
      userConditions: [{ id: 'u1', name: 'U', outputChannelId: 'ch-uc', statements: [] }],
      loggingDefinitions: [],
      enumDefinitions: [],
      fuelConfig: {
        isEnabled: false,
        tankCapacityGallons: 0,
        defaultConsumptionGalPerMin: 0,
        defaultYellowConsumptionMultiplier: 1,
        defaultCode35ConsumptionMultiplier: 1,
        fuelLevelChannelId: 'a2529acf-a7c6-449f-8a85-c7d76b35dbcb',
        tripFuelChannelId: 'acd3d127-acaf-4f8a-b27a-8623cfda09f3',
        fuelUsedChannelId: '740ce2a6-dc88-4425-85dc-7f99f2a902f1',
        fuelFullChannelId: 'c3b94831-95f6-4935-bf67-1aacfd611f75',
        inPitChannelId: 'da12563a-1167-4899-9956-700b0b693005',
        throttleConsumption: {
          isEnabled: false,
          maxRpm: 0,
          throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
          engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
        },
      },
    });

    const ids = service.usedChannelIds();
    expect(ids).toContain('ch-can');
    expect(ids).toContain('ch-alarm');
    expect(ids).toContain('ch-counter');
    expect(ids).toContain('ch-math');
    expect(ids).toContain('ch-table');
    expect(ids).toContain('ch-timer');
    expect(ids).toContain('ch-uc');
    expect(ids.length).toBe(7);
  });

  it('updateFromConfiguration clears usedChannelIds when config is null', () => {
    service.updateFromConfiguration(null);
    expect(service.usedChannelIds()).toEqual([]);
  });

  it('flags channelDefinitions with producedByFeature as used with "Output: <feature>" labels', () => {
    // Regression for Piece 1 of the feature-input refactor — feature-output channels
    // (FuelRange*, ThrottleProxy* outputs etc.) should appear in the usedChannelIds set so
    // the channel-selection-list filters/marks them as already-used.
    service.updateFromConfiguration({
      configurationId: 'cfg-2',
      configurationSchemaVersion: 1,
      name: 'Test',
      notes: '',
      lastUpdated: new Date(),
      lastUpdatedOnCarTimestamp: null,
      car: '77',
      isCloudConnectionEnabled: false,
      clientId: '',
      clientSecret: '',
      canConfig: { canBusEnabled: [], interfaces: [] },
      channelDefinitions: [
        {
          id: 'ch-fuel-range',
          isReserved: true,
          category: 'Fuel',
          name: 'FuelRangeMinutes',
          abbreviation: 'FLRMIN',
          dataType: 'Duration',
          baseUnitType: 'Minute',
          outputUnitType: 'Minute',
          outputDecimalPlaces: 1,
          lowRange: 0,
          highRange: 600,
          defaultValue: 0,
          groupTag: '',
          enumConversion: null,
          timeoutMs: 0,
          distribution: 2 /* CloudLocal */,
          isDistributionLocked: false,
          scope: 0 /* PerCar */,
          managedByFeature: 'fuel-analysis',
          producedByFeature: 'fuel-analysis',
        },
        {
          id: 'ch-tp-rate',
          isReserved: true,
          category: 'Fuel',
          name: 'ThrottleProxyRate',
          abbreviation: 'TPRATE',
          dataType: 'VolumeFlow',
          baseUnitType: 'UsGallonPerMinute',
          outputUnitType: 'UsGallonPerMinute',
          outputDecimalPlaces: 3,
          lowRange: 0,
          highRange: 10,
          defaultValue: 0,
          groupTag: '',
          enumConversion: null,
          timeoutMs: 0,
          distribution: 1 /* CarToCloud */,
          isDistributionLocked: true,
          scope: 0 /* PerCar */,
          managedByFeature: 'throttle-consumption',
          producedByFeature: 'throttle-consumption',
        },
        {
          // No producedByFeature — should NOT appear in the used set from this scan.
          id: 'ch-plain',
          isReserved: true,
          category: 'Car Temps',
          name: 'CoolantTemp',
          abbreviation: 'COOLNT',
          dataType: 'Temperature',
          baseUnitType: 'DegreeFahrenheit',
          outputUnitType: 'DegreeFahrenheit',
          outputDecimalPlaces: 1,
          lowRange: 0,
          highRange: 300,
          defaultValue: 0,
          groupTag: '',
          enumConversion: null,
          timeoutMs: 0,
          distribution: 1 /* CarToCloud */,
          isDistributionLocked: false,
          scope: 0 /* PerCar */,
          managedByFeature: null,
          producedByFeature: null,
        },
      ],
      alarmDefinitions: [],
      counterDefinitions: [],
      mathDefinitions: [],
      tableDefinitions: [],
      timerDefinitions: [],
      userConditions: [],
      loggingDefinitions: [],
      enumDefinitions: [],
      fuelConfig: {
        isEnabled: false,
        tankCapacityGallons: 0,
        defaultConsumptionGalPerMin: 0,
        defaultYellowConsumptionMultiplier: 1,
        defaultCode35ConsumptionMultiplier: 1,
        fuelLevelChannelId: 'a2529acf-a7c6-449f-8a85-c7d76b35dbcb',
        tripFuelChannelId: 'acd3d127-acaf-4f8a-b27a-8623cfda09f3',
        fuelUsedChannelId: '740ce2a6-dc88-4425-85dc-7f99f2a902f1',
        fuelFullChannelId: 'c3b94831-95f6-4935-bf67-1aacfd611f75',
        inPitChannelId: 'da12563a-1167-4899-9956-700b0b693005',
        throttleConsumption: {
          isEnabled: false,
          maxRpm: 0,
          throttlePositionChannelId: 'c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01',
          engineRpmChannelId: '74c57a58-d78d-499a-977b-11cee221926a',
        },
      },
    });

    const usedIds = service.usedChannelIds();
    expect(usedIds).toContain('ch-fuel-range');
    expect(usedIds).toContain('ch-tp-rate');
    expect(usedIds).not.toContain('ch-plain');

    const usageMap = service.channelUsageMap();
    expect(usageMap.get('ch-fuel-range')).toEqual(['Output: Fuel Analysis']);
    expect(usageMap.get('ch-tp-rate')).toEqual(['Output: Throttle Consumption']);
  });
});
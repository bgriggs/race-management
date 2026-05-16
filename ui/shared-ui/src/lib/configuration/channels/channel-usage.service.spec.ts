import { ChannelUsageService } from './channel-usage.service';
import { CanBusInterfaceConfig } from '../../../models/can-bus-interface-config';
import { ConditionDefinition } from '../../../models/condition-definition';
import { MathDefinition } from '../../../models/math-definition';
import { CounterDefinition } from '../../../models/counter-definition';
import { TimerDefinition } from '../../../models/timer-definition';
import { TableDefinition } from '../../../models/table-definition';
import { AlarmDefinition } from '../../../models/alarm-definition';

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
      { id: 'm1', name: 'M1', type: 'SimpleOperation', a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: 'ch-out-a', simpleOperationType: 'Add' },
      { id: 'm2', name: 'M2', type: 'SimpleOperation', a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: 'ch-out-b', simpleOperationType: 'Add' },
      { id: 'm3', name: 'M3', type: 'SimpleOperation', a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: 'ch-out-a', simpleOperationType: 'Add' },
      { id: 'm4', name: 'M4', type: 'SimpleOperation', a: 0, b: 0, channel1Id: 'ch-input', channel2Id: null, outputChannelId: '00000000-0000-0000-0000-000000000000', simpleOperationType: 'Add' },
    ];

    expect(service.getUsedChannelIdsFromMathDefinitions(defs)).toEqual(['ch-out-a', 'ch-out-b']);
  });

  it('tracks counter definition output channels, ignores empty guid', () => {
    const defs: Partial<CounterDefinition>[] = [
      { id: 'c1', outputChId: 'ch-counter-a', upChId: 'ch-up', downChId: '00000000-0000-0000-0000-000000000000', resetChId: '00000000-0000-0000-0000-000000000000' },
      { id: 'c2', outputChId: '00000000-0000-0000-0000-000000000000', upChId: 'ch-up', downChId: '00000000-0000-0000-0000-000000000000', resetChId: '00000000-0000-0000-0000-000000000000' },
    ] as CounterDefinition[];

    expect(service.getUsedChannelIdsFromCounterDefinitions(defs)).toEqual(['ch-counter-a']);
  });

  it('tracks timer definition output channels, ignores empty guid', () => {
    const defs: Partial<TimerDefinition>[] = [
      { id: 't1', outputChId: 'ch-timer-a', statement: { comparisons: [], logicType: 'And' } },
      { id: 't2', outputChId: '00000000-0000-0000-0000-000000000000', statement: { comparisons: [], logicType: 'And' } },
    ] as TimerDefinition[];

    expect(service.getUsedChannelIdsFromTimerDefinitions(defs)).toEqual(['ch-timer-a']);
  });

  it('tracks table definition output channels, ignores empty guid', () => {
    const defs: Partial<TableDefinition>[] = [
      { id: 'tbl1', outputChannel: 'ch-table-a', inputChannel: 'ch-in', mappings: [] },
      { id: 'tbl2', outputChannel: '00000000-0000-0000-0000-000000000000', inputChannel: 'ch-in', mappings: [] },
    ] as TableDefinition[];

    expect(service.getUsedChannelIdsFromTableDefinitions(defs)).toEqual(['ch-table-a']);
  });

  it('tracks alarm definition status output channels, ignores null and empty guid', () => {
    const defs: Partial<AlarmDefinition>[] = [
      { id: 'a1', alarmStatusChannelId: 'ch-alarm-a', statement: { comparisons: [], logicType: 'And' } },
      { id: 'a2', alarmStatusChannelId: null, statement: { comparisons: [], logicType: 'And' } },
      { id: 'a3', alarmStatusChannelId: '00000000-0000-0000-0000-000000000000', statement: { comparisons: [], logicType: 'And' } },
    ] as AlarmDefinition[];

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
      alarmDefinitions: [{ id: 'a1', name: 'A', statement: { comparisons: [], logicType: 'And' }, messsage: '', displayChannelSourceColorHex: '', timeAfterAckToDisplaySecs: 0, alarmStatusChannelId: 'ch-alarm' }],
      counterDefinitions: [{ id: 'c1', name: 'C', outputChId: 'ch-counter', upChId: '00000000-0000-0000-0000-000000000000', downChId: '00000000-0000-0000-0000-000000000000', resetChId: '00000000-0000-0000-0000-000000000000', startValue: 0, enableMinClamp: false, minClampValue: 0, enableMaxClamp: false, maxClampValue: 0 }],
      mathDefinitions: [{ id: 'm1', name: 'M', type: 'SimpleOperation', a: 0, b: 0, channel1Id: 'ch-can', channel2Id: null, outputChannelId: 'ch-math', simpleOperationType: 'Add' }],
      tableDefinitions: [{ id: 'tbl1', name: 'T', isEnum: false, ignoreCase: false, inputChannel: 'ch-can', outputChannel: 'ch-table', interpolationType: 'Linear', mappings: [] }],
      timerDefinitions: [{ id: 't1', name: 'T', outputChId: 'ch-timer', statement: { comparisons: [], logicType: 'And' }, countDown: false, enableRollover: false, rolloverSeconds: 0, enableStartSeconds: false, startSeconds: 0, enableStopSeconds: false, stopSeconds: 0 }],
      userConditions: [{ id: 'u1', name: 'U', outputChannelId: 'ch-uc', statements: [] }],
      loggingDefinitions: [],
      enumDefinitions: [],
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
});
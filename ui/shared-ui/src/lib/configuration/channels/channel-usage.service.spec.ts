import { ChannelUsageService } from './channel-usage.service';
import { CanBusInterfaceConfig } from '../../../models/can-bus-interface-config';
import { ConditionDefinition } from '../../../models/condition-definition';

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
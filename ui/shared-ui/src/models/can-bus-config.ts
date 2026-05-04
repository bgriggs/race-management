/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanBusInterfaceConfig } from "./can-bus-interface-config";

/**
 * Top level CAN bus configuration, which includes a list of CAN bus interfaces and their settings, as well as a list of booleans indicating whether each CAN bus is enabled.
 */
export interface CanBusConfig {
    canBusEnabled: boolean[];
    interfaces: CanBusInterfaceConfig[];
}

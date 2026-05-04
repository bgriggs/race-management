/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanMessageConfig } from "./can-message-config";

/**
 * Represents settings for a single CAN network interface.
 */
export interface CanBusInterfaceConfig {
    /**
     * Network interface name, such as "can0." The application will attempt to connect to this interface and read/write CAN messages according to the configuration.
     */
    interfaceName: string;
    bitRate: number;
    silentOnCanBus: boolean;
    messages: CanMessageConfig[];
}

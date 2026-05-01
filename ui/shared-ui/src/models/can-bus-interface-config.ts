/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanMessageConfig } from "./can-message-config";

export interface CanBusInterfaceConfig {
    interfaceName: string;
    bitRate: number;
    silentOnCanBus: boolean;
    messages: CanMessageConfig[];
}

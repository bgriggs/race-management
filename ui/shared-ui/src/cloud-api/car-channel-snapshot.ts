/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ChannelValueSnapshot } from "./channel-value-snapshot";

export interface CarChannelSnapshot {
    carKey: string;
    carNumber: string;
    channels: { [key: number]: ChannelValueSnapshot; };
    configurationId: string | null;
}

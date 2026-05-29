/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { StintOriginType } from "./stint-origin-type";

export interface CreateStintRequest {
    carNumber: string;
    raceId: number;
    startAt: Date;
    endAt: Date | null;
    originType: StintOriginType;
}

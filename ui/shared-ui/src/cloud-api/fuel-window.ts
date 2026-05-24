/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface FuelWindow {
    id: number;
    teamId: number;
    carNumber: string;
    raceId: number;
    startRefuelEventId: number;
    endRefuelEventId: number | null;
    openedAt: Date;
    closedAt: Date | null;
    observedConsumptionGalPerMin: number | null;
    observedDurationSeconds: number | null;
    closedBySessionEnd: boolean;
}

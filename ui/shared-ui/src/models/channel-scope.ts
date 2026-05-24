/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

/**
 * What entity a channel's values are bound to. PerCar values are keyed by
 * (TeamId, CarId, ChannelId); PerTeam values are keyed by (TeamId, ChannelId)
 * and shared across every car on the team (e.g., a race-wide flag state).
 */
export enum ChannelScope {
    /**
     * Value belongs to a specific car (default).
     */
    PerCar = 0,
    /**
     * Value is shared across all cars on a team.
     */
    PerTeam = 1,
}

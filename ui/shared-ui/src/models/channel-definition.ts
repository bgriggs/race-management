/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ChannelDistribution } from "./channel-distribution";
import { ChannelScope } from "./channel-scope";

/**
 * Channel metadata definition, which defines the properties of a channel, such as its name, data type, and units.
 */
export interface ChannelDefinition {
    /**
     * Globally unique identifier for this channel, stable across all tiers (car, cloud, local config)
     * without requiring a central authority. Generated once at definition time and never changed.
     */
    id: string;
    isReserved: boolean;
    category: string;
    name: string;
    abbreviation: string;
    /**
     * Gets or sets the data type such as: Temperature, Length, Volume, VolumeFlow, Duration, Speed, Pressure, Force, Voltage, Mass, Ratio, Current, Resistance.
     * Special types: Unitless, String
     */
    dataType: string;
    /**
     * Gets or sets the base unit type for the channel, such as degrees, feet, etc. When the value is set, this is its units.
     */
    baseUnitType: string;
    /**
     * Gets or sets the type of unit used when the channel value is accessed such as for displaying values.
     */
    outputUnitType: string;
    outputDecimalPlaces: number;
    lowRange: number;
    highRange: number;
    defaultValue: number;
    groupTag: string;
    /**
     * Optional conversion from the value to specified string enum values, stored as a reference to a separate enum conversion definition. 
     * This allows for mapping numeric values to human-readable strings, such as mapping 0, 1, 2 to "Off", "On", "Auto" for a channel that
     * represents a mode setting. The enum conversion definition would define the mapping of numeric values to string values for the channel.
     */
    enumConversion: string | null;
    /**
     * Amount of time in milliseconds between updates from the channel source before considering the value timed out and set to default.
     */
    timeoutMs: number;
    /**
     * Where the channel's values are produced and which tiers they are transmitted to.
     * Defaults to @see {@link Channels.ChannelDistribution.CarToCloud}, matching today's behavior
     * for car-side telemetry.
     */
    distribution: ChannelDistribution;
    /**
     * What entity the channel's values are bound to. Defaults to @see {@link Channels.ChannelScope.PerCar}.
     */
    scope: ChannelScope;
    /**
     * Identifier of the feature that owns this channel's lifecycle (e.g., "fuel-analysis",
     * "throttle-consumption"). When non-null on a reserved-channel template, the channel
     * is auto-injected into a car configuration when its feature is enabled, hidden from
     * the user's reserved-channel picker, and removed when the feature is disabled. The
     * value propagates to the per-car channel instance at injection time, where the UI
     * uses it to lock editing and deletion.
     */
    managedByFeature: string | null;
}

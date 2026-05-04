/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

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
     * Non-numeric types such as enums and table strings.
     */
    isStringValue: boolean;
    /**
     * Gets or sets the data type such as: Temperature, Length, Volume, VolumeFlow, Duration, Speed, Pressure, Force, Voltage, Mass, Ratio, Current, Resistance.
     */
    dataType: string;
    /**
     * Gets or sets the base unit type for the channel, such as degrees, feet, etc. When the value is set, this is its units.
     */
    baseUnitType: string;
    baseDecimalPlaces: number;
    /**
     * Gets or sets the type of unit used when the channel value is accessed such as for displaying values.
     */
    outputUnitType: string;
    outputDecimalPlaces: number;
    lowRange: number;
    highRange: number;
    groupTag: string;
}

/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { EnumValueDefinition } from "./enum-value-definition";

/**
 * Mapping from a raw channel value to a display string. This allows for channels to be displayed as discrete values instead of just numbers. 
 * For example, a channel that outputs 0 or 1 could have an enum definition that maps 0 to "Off" and 1 to "On".
 */
export interface EnumDefinition {
    /**
     * Gets or sets the unique identifier.
     */
    id: string;
    /**
     * Name of the enum such as "Pump State". Maximum length is 20 characters.
     */
    name: string;
    /**
     * Mapping of raw channel values to display strings. The Source property is the raw value as a string, and the Value property is the integer 
     * value that the channel outputs. For example, for a channel that outputs 0 or 1, you could have an EnumValueDefinition with Source = "Off" 
     * and Value = 0, and another EnumValueDefinition with Source = "On" and Value = 1.
     */
    values: EnumValueDefinition[];
}

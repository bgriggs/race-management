/**
 * A racecar management API endpoint discovered on the local network via DNS-SD (_racecar._tcp).
 */
export interface DiscoveredRacecar {
  /** DNS-SD service name — the car's Pi hostname (e.g. "race-car-1"). */
  name: string;
  /** mDNS hostname or IP address (e.g. "192.168.1.42"). */
  host: string;
  /** HTTP port of the management REST API. */
  port: number;
  /** Computed base URL for the management REST API. */
  baseUrl: string;
}

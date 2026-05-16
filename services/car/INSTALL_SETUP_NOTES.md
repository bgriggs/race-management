# Car Install Setup Notes (Raspberry Pi)

## System packages
Install base packages used by the Racecar service:

```bash
sudo apt update
sudo apt install -y avahi-daemon avahi-utils can-utils
```

## DNS-SD discovery (avahi-publish)
The Racecar app advertises `_racecar._tcp` on Linux using `avahi-publish`.

### Verify installation
```bash
which avahi-publish
avahi-publish --help | head -n 1
```

If `avahi-publish` is missing, install:
```bash
sudo apt install -y avahi-utils
```

### Ensure Avahi daemon is running
```bash
sudo systemctl enable avahi-daemon
sudo systemctl start avahi-daemon
sudo systemctl status avahi-daemon
```

## CAN interface (PICAN2)
Use the PICAN2 setup guide and driver steps from Copperhill:

https://copperhilltech.com/blog/pican2-pican3-and-picanm-driver-installation-for-raspberry-pi/

### Quick verification after setup
```bash
# Check SPI and MCP2515/can modules are available
lsmod | egrep 'spi|mcp251x|can'

# Example interface bring-up (adjust bitrate as needed)
sudo ip link set can0 up type can bitrate 500000
ip -details link show can0
```

## Helpful diagnostics
```bash
# Check mDNS service browse
avahi-browse -art

# Watch system logs for avahi and racecar service
journalctl -u avahi-daemon -f
journalctl -u racecar -f
```

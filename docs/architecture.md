# Architecture

## Tracking data path

Camera / Lens
      ↓
Serial / RS-485
      ↓
STM32
      ↓
Tracking Decoder
      ↓
Free-D Encoder
      ↓
Ethernet (W5500)
      ↓
Tracking Destination

The embedded firmware receives camera and lens tracking data through a serial interface, decodes the source packet, applies configurable coordinate transformation and synthetic calibration tables, encodes a Free-D packet, and transmits it over Ethernet.

## Monitoring and configuration path

Tracking Monitor
       ↕
Configuration / Diagnostics
       ↕
Tracking Box

The .NET desktop monitor reads telemetry, presents connection and health information, and provides guarded configuration operations. The public defaults use RFC 5737 documentation addresses and do not identify an operational network.

## Repository boundaries

Vendor HAL, CMSIS, Ethernet libraries, installation calibration, logs, binaries, and deployment configuration are intentionally not bundled in this portfolio snapshot. See THIRD_PARTY_NOTICES.md for dependency categories.

# Security and publication policy

- Public network examples use only RFC 5737 documentation addresses.
- The locally administered example MAC address is 02:00:00:00:00:10.
- Local maintenance credentials are not versioned.
- The monitor derives password hashes with PBKDF2-SHA256 and a random salt, stored under the user's local application-data directory.
- Operational logs and diagnostic captures are not versioned.
- Installation calibration, physical geometry, offsets, and maps are not versioned.
- Compiled firmware, executables, libraries, symbols, archives, and build outputs are not versioned.
- Screenshots containing operational data are not included.

The values under config-examples and the calibration tables in the public firmware copy are synthetic and intended only to explain the architecture and support isolated testing.

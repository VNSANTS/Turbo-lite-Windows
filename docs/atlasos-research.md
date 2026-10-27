# AtlasOS research and Turbo Lite Windows decisions

Research date: 2026-10-27

## Official project
- Repository: https://github.com/Atlas-OS/Atlas
- Documentation: https://docs.atlasos.net/getting-started/install/install-playbook/
- Latest listed release during research: Atlas v0.5.0-hotfix (2025-10-21)

## What Atlas does
AtlasOS is an open-source Windows modification applied through the AME Wizard and an Atlas Playbook. It targets performance, privacy, and usability. It removes commonly unnecessary apps, adjusts Windows settings and policies, and exposes optional security-related choices. The playbook is inspectable; Atlas does not distribute a modified Windows ISO.

## Critical compatibility warning
The current official installation guide requires a clean Windows reinstall and says supported systems must meet Windows 11 requirements. The v0.5.0 release notes explicitly say Windows 10 support was removed. Installing Atlas on the user's current Windows 10 PC is therefore not the supported route. The project also warns that uninstalling Atlas requires reinstalling Windows.

## What Turbo Lite can safely adopt
1. Modular, auditable optimization actions rather than opaque one-click scripts.
2. Explicit descriptions of each change, its rationale, risk, and compatibility.
3. Selective removal of reinstallable, nonessential apps.
4. Optional privacy and usability settings with security trade-offs disclosed.
5. Capture previous state and logs before changes; provide rollback only where technically reliable.
6. Measure before/after behavior instead of promising unverified FPS or RAM gains.

## Turbo Lite implementation policy
- Do not embed or blindly execute the full Atlas Playbook.
- Do not disable Defender, firewall, Windows Update, UAC, or CPU mitigations by default.
- Do not remove services or components in bulk.
- Check Windows edition/build and architecture before enabling a module.
- First implement read-only diagnostics, then low-risk reversible settings, then individually reviewed advanced actions.
- Provide a clear warning and explicit confirmation for actions that can break features or require reinstall to recover.

## Sources
- https://github.com/Atlas-OS/Atlas
- https://github.com/Atlas-OS/Atlas/releases
- https://docs.atlasos.net/getting-started/install/install-playbook/
- https://docs.atlasos.net/faq/general-faq/installation-data-loss/
